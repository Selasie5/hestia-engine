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
    private readonly ICacheService _cacheService;

    public GetAvailableRoomsQueryHandler(IRoomRepository roomRepository, ICacheService cacheService)
    {
        _roomRepository = roomRepository;
        _cacheService = cacheService;
    }

    public async Task<List<RoomDto>> Handle(GetAvailableRoomsQuery query, CancellationToken ct)
    {
        var cacheKey = $"rooms:available:{query.HostelId}";

        var cached = await _cacheService.GetAsync<List<RoomDto>>(cacheKey, ct);
        if (cached is not null)
            return cached;

        var rooms = await _roomRepository.GetAvailableByHostelIdAsync(query.HostelId, ct);

        var result = rooms.Select(r => new RoomDto(
            r.Id,
            r.RoomNumber,
            r.Capacity,
            r.CurrentOccupancy,
            r.PricePerSemester,
            r.IsAvailable,
            r.HostelId,
            r.Hostel?.Name ?? "N/A"
        )).ToList();

        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromSeconds(60), ct);

        return result;
    }
}
