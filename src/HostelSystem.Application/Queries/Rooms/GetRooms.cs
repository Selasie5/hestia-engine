using HostelSystem.Application.Common;
using HostelSystem.Application.DTOs;
using HostelSystem.Application.Interfaces;
using MediatR;

namespace HostelSystem.Application.Queries.Rooms;

public record GetRoomsQuery(int? HostelId, bool? IsAvailable, string? Search, int Page = 1, int PageSize = 20) : IRequest<PagedResult<RoomDto>>;
public record GetRoomByIdQuery(int Id) : IRequest<Result<RoomDto>>;

public class GetRoomsHandler : IRequestHandler<GetRoomsQuery, PagedResult<RoomDto>>
{
    private readonly IRoomRepository _repo;
    public GetRoomsHandler(IRoomRepository repo) { _repo = repo; }

    public async Task<PagedResult<RoomDto>> Handle(GetRoomsQuery q, CancellationToken ct)
    {
        var (items, total) = await _repo.GetPagedAsync(q.HostelId, q.IsAvailable, q.Search, q.Page, q.PageSize, ct);
        var dtos = items.Select(r => new RoomDto(r.Id, r.RoomNumber, r.Capacity, r.CurrentOccupancy, r.PricePerSemester, r.IsAvailable, r.HostelId, r.Hostel?.Name ?? "N/A")).ToList();
        return new PagedResult<RoomDto>(dtos, total, q.Page, q.PageSize);
    }
}

public class GetRoomByIdHandler : IRequestHandler<GetRoomByIdQuery, Result<RoomDto>>
{
    private readonly IRoomRepository _repo;
    public GetRoomByIdHandler(IRoomRepository repo) { _repo = repo; }
    public async Task<Result<RoomDto>> Handle(GetRoomByIdQuery q, CancellationToken ct)
    {
        var r = await _repo.GetByIdAsync(q.Id, ct);
        if (r is null) return Result<RoomDto>.Fail("Room not found.");
        return Result<RoomDto>.Ok(new RoomDto(r.Id, r.RoomNumber, r.Capacity, r.CurrentOccupancy, r.PricePerSemester, r.IsAvailable, r.HostelId, r.Hostel?.Name ?? "N/A"));
    }
}
