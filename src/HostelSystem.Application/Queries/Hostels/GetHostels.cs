using HostelSystem.Application.Common;
using HostelSystem.Application.DTOs;
using HostelSystem.Application.Interfaces;
using MediatR;

namespace HostelSystem.Application.Queries.Hostels;

public record GetHostelsQuery(bool OnlyActive = false, int Page = 1, int PageSize = 20) : IRequest<PagedResult<HostelDto>>;
public record GetHostelByIdQuery(int Id) : IRequest<Result<HostelDto>>;

public class GetHostelsHandler : IRequestHandler<GetHostelsQuery, PagedResult<HostelDto>>
{
    private readonly IHostelRepository _repo;
    private readonly ICacheService _cache;
    public GetHostelsHandler(IHostelRepository repo, ICacheService cache) { _repo = repo; _cache = cache; }

    public async Task<PagedResult<HostelDto>> Handle(GetHostelsQuery q, CancellationToken ct)
    {
        var cacheKey = q.OnlyActive ? $"hostels:active:p{q.Page}:s{q.PageSize}" : $"hostels:all:p{q.Page}:s{q.PageSize}";
        var cached = await _cache.GetAsync<PagedResult<HostelDto>>(cacheKey, ct);
        if (cached is not null) return cached;

        var (items, total) = await _repo.GetPagedAsync(q.OnlyActive, q.Page, q.PageSize, ct);
        var dtos = items.Select(h => new HostelDto(h.Id, h.Name, h.Description, h.Address, h.IsActive, h.Rooms.Sum(r => r.Capacity), h.Rooms.Sum(r => r.CurrentOccupancy), h.Rooms.Sum(r => r.Capacity) - h.Rooms.Sum(r => r.CurrentOccupancy))).ToList();
        var result = new PagedResult<HostelDto>(dtos, total, q.Page, q.PageSize);
        await _cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5), ct);
        return result;
    }
}

public class GetHostelByIdHandler : IRequestHandler<GetHostelByIdQuery, Result<HostelDto>>
{
    private readonly IHostelRepository _repo;
    private readonly ICacheService _cache;
    public GetHostelByIdHandler(IHostelRepository repo, ICacheService cache) { _repo = repo; _cache = cache; }
    public async Task<Result<HostelDto>> Handle(GetHostelByIdQuery q, CancellationToken ct)
    {
        var cacheKey = $"hostel:{q.Id}";
        var cached = await _cache.GetAsync<HostelDto>(cacheKey, ct);
        if (cached is not null) return Result<HostelDto>.Ok(cached);

        var h = await _repo.GetByIdAsync(q.Id, ct);
        if (h is null) return Result<HostelDto>.Fail("Hostel not found.");
        var dto = new HostelDto(h.Id, h.Name, h.Description, h.Address, h.IsActive, h.Rooms.Sum(r => r.Capacity), h.Rooms.Sum(r => r.CurrentOccupancy), h.Rooms.Sum(r => r.Capacity) - h.Rooms.Sum(r => r.CurrentOccupancy));
        await _cache.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(5), ct);
        return Result<HostelDto>.Ok(dto);
    }
}
