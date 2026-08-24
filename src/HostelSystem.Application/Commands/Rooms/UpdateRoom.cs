using HostelSystem.Application.Common;
using HostelSystem.Application.DTOs;
using HostelSystem.Application.Interfaces;
using MediatR;

namespace HostelSystem.Application.Commands.Rooms;

public record UpdateRoomCommand(int Id, string RoomNumber, int Capacity, decimal PricePerSemester, Guid? Version) : IRequest<Result<RoomDto>>;
public record DeleteRoomCommand(int Id) : IRequest<Result<bool>>;

public class UpdateRoomHandler : IRequestHandler<UpdateRoomCommand, Result<RoomDto>>
{
    private readonly IRoomRepository _roomRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICacheService _cache;

    public UpdateRoomHandler(IRoomRepository roomRepo, IUnitOfWork uow, ICacheService cache)
    { _roomRepo = roomRepo; _uow = uow; _cache = cache; }

    public async Task<Result<RoomDto>> Handle(UpdateRoomCommand cmd, CancellationToken ct)
    {
        var room = await _roomRepo.GetByIdAsync(cmd.Id, ct);
        if (room is null) return Result<RoomDto>.Fail("Room not found.");

        if (cmd.Version.HasValue && room.Version != cmd.Version.Value)
            return Result<RoomDto>.Fail("Conflict: room was modified by another request (Version mismatch).");

        if (!room.RoomNumber.Equals(cmd.RoomNumber.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            var dup = await _roomRepo.ExistsByRoomNumberAsync(room.HostelId, cmd.RoomNumber.Trim(), cmd.Id, ct);
            if (dup) return Result<RoomDto>.Fail($"RoomNumber {cmd.RoomNumber} already exists in this hostel.");
        }

        if (cmd.Capacity < room.CurrentOccupancy)
            return Result<RoomDto>.Fail($"Capacity {cmd.Capacity} cannot be less than current occupancy {room.CurrentOccupancy}.");

        try { room.UpdateDetails(cmd.RoomNumber.Trim(), cmd.Capacity, cmd.PricePerSemester); }
        catch (Exception ex) { return Result<RoomDto>.Fail(ex.Message); }

        _roomRepo.Update(room);
        try { await _uow.SaveChangesAsync(ct); }
        catch (Exception ex) when (ex.Message.Contains("concurrency", StringComparison.OrdinalIgnoreCase) || ex.InnerException?.Message.Contains("concurrency", StringComparison.OrdinalIgnoreCase) == true)
        { return Result<RoomDto>.Fail("Conflict: concurrent update (retry)."); }

        await _cache.RemoveAsync($"rooms:available:{room.HostelId}", ct);

        return Result<RoomDto>.Ok(new RoomDto(room.Id, room.RoomNumber, room.Capacity, room.CurrentOccupancy, room.PricePerSemester, room.IsAvailable, room.HostelId, room.Hostel?.Name ?? "N/A"));
    }
}

public class DeleteRoomHandler : IRequestHandler<DeleteRoomCommand, Result<bool>>
{
    private readonly IRoomRepository _roomRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICacheService _cache;
    public DeleteRoomHandler(IRoomRepository roomRepo, IUnitOfWork uow, ICacheService cache) { _roomRepo = roomRepo; _uow= uow; _cache = cache; }
    public async Task<Result<bool>> Handle(DeleteRoomCommand cmd, CancellationToken ct)
    {
        var room = await _roomRepo.GetByIdAsync(cmd.Id, ct);
        if (room is null) return Result<bool>.Fail("Room not found.");
        if (room.CurrentOccupancy > 0) return Result<bool>.Fail("Cannot delete an occupied room.");
        _roomRepo.Delete(room);
        try { await _uow.SaveChangesAsync(ct); }
        catch (Exception ex) { return Result<bool>.Fail(ex.Message); }
        await _cache.RemoveAsync($"rooms:available:{room.HostelId}", ct);
        return Result<bool>.Ok(true);
    }
}
