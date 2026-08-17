using MediatR;
using HostelSystem.Application.DTOs;
using HostelSystem.Application.Interfaces;

namespace HostelSystem.Application.Queries.Rooms;

// ============================================================
// TEMPLATE for a Query — read-only, bypasses domain, optimized
// ============================================================

public record GetAvailableRoomsQuery(int HostelId) : IRequest<List<RoomDto>>;

public class GetAvailableRoomsQueryHandler : IRequestHandler<GetAvailableRoomsQuery, List<RoomDto>>
{
    private readonly IRoomRepository _roomRepository;

    public GetAvailableRoomsQueryHandler(IRoomRepository roomRepository)
    {
        _roomRepository = roomRepository;
    }

    public async Task<List<RoomDto>> Handle(GetAvailableRoomsQuery query, CancellationToken ct)
    {
        var rooms = await _roomRepository.GetAvailableByHostelIdAsync(query.HostelId, ct);

        return rooms.Select(r => new RoomDto(
            r.Id,
            r.RoomNumber,
            r.Capacity,
            r.CurrentOccupancy,
            r.PricePerSemester,
            r.IsAvailable,
            r.HostelId,
            r.Hostel?.Name ?? "N/A"
        )).ToList();
    }
}
