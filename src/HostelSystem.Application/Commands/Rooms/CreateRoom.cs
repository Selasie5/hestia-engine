using HostelSystem.Application.Common;
using HostelSystem.Application.DTOs;
using HostelSystem.Application.Interfaces;
using HostelSystem.Domain.Entities;
using MediatR;

namespace HostelSystem.Application.Commands.Rooms;

public record CreateRoomCommand(string RoomNumber, int Capacity, decimal PricePerSemester, int HostelId) : IRequest<Result<RoomDto>>;

public class CreateRoomHandler : IRequestHandler<CreateRoomCommand, Result<RoomDto>>
{
    private readonly IHostelRepository _hostelRepo;
    private readonly IRoomRepository _roomRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICacheService _cache;

    public CreateRoomHandler(IHostelRepository hostelRepo, IRoomRepository roomRepo, IUnitOfWork uow, ICacheService cache)
    {
        _hostelRepo = hostelRepo; _roomRepo = roomRepo; _uow = uow; _cache = cache;
    }

    public async Task<Result<RoomDto>> Handle(CreateRoomCommand cmd, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.RoomNumber)) return Result<RoomDto>.Fail("RoomNumber is required.");
        if (cmd.Capacity <= 0) return Result<RoomDto>.Fail("Capacity must be > 0.");
        if (cmd.PricePerSemester <= 0) return Result<RoomDto>.Fail("Price must be > 0.");

        var hostel = await _hostelRepo.GetByIdAsync(cmd.HostelId, ct);
        if (hostel is null) return Result<RoomDto>.Fail("Hostel not found.");
        if (!hostel.IsActive) return Result<RoomDto>.Fail("Hostel is deactivated.");

        var exists = await _roomRepo.ExistsByRoomNumberAsync(cmd.HostelId, cmd.RoomNumber.Trim(), null, ct);
        if (exists) return Result<RoomDto>.Fail($"RoomNumber {cmd.RoomNumber} already exists in this hostel.");

        Room room;
        try { room = new Room(cmd.RoomNumber.Trim(), cmd.Capacity, cmd.PricePerSemester, cmd.HostelId); }
        catch (Exception ex) { return Result<RoomDto>.Fail(ex.Message); }

        await _roomRepo.AddAsync(room, ct);
        try { await _uow.SaveChangesAsync(ct); }
        catch (Exception ex) when (ex.InnerException?.Message.Contains("UNIQUE") == true || ex.Message.Contains("UNIQUE"))
        { return Result<RoomDto>.Fail("RoomNumber already exists (concurrent)."); }

        await _cache.RemoveAsync($"rooms:available:{cmd.HostelId}", ct);
        await _cache.RemoveAsync("hostels:all", ct); await _cache.RemoveAsync("hostels:active", ct);

        var dto = new RoomDto(room.Id, room.RoomNumber, room.Capacity, room.CurrentOccupancy, room.PricePerSemester, room.IsAvailable, room.HostelId, hostel.Name);
        return Result<RoomDto>.Ok(dto);
    }
}
