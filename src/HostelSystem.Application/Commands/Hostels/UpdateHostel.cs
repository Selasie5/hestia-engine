using HostelSystem.Application.Common;
using HostelSystem.Application.DTOs;
using HostelSystem.Application.Interfaces;
using MediatR;

namespace HostelSystem.Application.Commands.Hostels;

public record UpdateHostelCommand(int Id, string Name, string Address, string? Description) : IRequest<Result<HostelDto>>;
public record DeactivateHostelCommand(int Id) : IRequest<Result<HostelDto>>;
public record ActivateHostelCommand(int Id) : IRequest<Result<HostelDto>>;

public class UpdateHostelHandler : IRequestHandler<UpdateHostelCommand, Result<HostelDto>>
{
    private readonly IHostelRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly ICacheService _cache;
    public UpdateHostelHandler(IHostelRepository repo, IUnitOfWork uow, ICacheService cache) { _repo = repo; _uow = uow; _cache = cache; }
    public async Task<Result<HostelDto>> Handle(UpdateHostelCommand cmd, CancellationToken ct)
    {
        var hostel = await _repo.GetByIdAsync(cmd.Id, ct);
        if (hostel is null) return Result<HostelDto>.Fail("Hostel not found.");
        try { hostel.UpdateDetails(cmd.Name.Trim(), cmd.Address.Trim(), cmd.Description?.Trim()); }
        catch (Exception ex) { return Result<HostelDto>.Fail(ex.Message); }
        _repo.Update(hostel);
        await _uow.SaveChangesAsync(ct);
        await _cache.RemoveAsync("hostels:active", ct);
        await _cache.RemoveAsync("hostels:all", ct);
        await _cache.RemoveAsync($"hostel:{hostel.Id}", ct);
        return Result<HostelDto>.Ok(new HostelDto(hostel.Id, hostel.Name, hostel.Description, hostel.Address, hostel.IsActive, hostel.TotalCapacity, hostel.TotalOccupancy, hostel.AvailableSpots));
    }
}

public class DeactivateHostelHandler : IRequestHandler<DeactivateHostelCommand, Result<HostelDto>>
{
    private readonly IHostelRepository _repo; private readonly IUnitOfWork _uow; private readonly ICacheService _cache;
    public DeactivateHostelHandler(IHostelRepository repo, IUnitOfWork uow, ICacheService cache) { _repo = repo; _uow = uow; _cache = cache; }
    public async Task<Result<HostelDto>> Handle(DeactivateHostelCommand cmd, CancellationToken ct)
    {
        var h = await _repo.GetByIdAsync(cmd.Id, ct);
        if (h is null) return Result<HostelDto>.Fail("Hostel not found.");
        try { h.Deactivate(); } catch (Exception ex) { return Result<HostelDto>.Fail(ex.Message); }
        _repo.Update(h); await _uow.SaveChangesAsync(ct);
        await _cache.RemoveAsync("hostels:active", ct); await _cache.RemoveAsync("hostels:all", ct);
        return Result<HostelDto>.Ok(new HostelDto(h.Id, h.Name, h.Description, h.Address, h.IsActive, h.TotalCapacity, h.TotalOccupancy, h.AvailableSpots));
    }
}

public class ActivateHostelHandler : IRequestHandler<ActivateHostelCommand, Result<HostelDto>>
{
    private readonly IHostelRepository _repo; private readonly IUnitOfWork _uow; private readonly ICacheService _cache;
    public ActivateHostelHandler(IHostelRepository repo, IUnitOfWork uow, ICacheService cache) { _repo = repo; _uow = uow; _cache = cache; }
    public async Task<Result<HostelDto>> Handle(ActivateHostelCommand cmd, CancellationToken ct)
    {
        var h = await _repo.GetByIdAsync(cmd.Id, ct);
        if (h is null) return Result<HostelDto>.Fail("Hostel not found.");
        try { h.Activate(); } catch (Exception ex) { return Result<HostelDto>.Fail(ex.Message); }
        _repo.Update(h); await _uow.SaveChangesAsync(ct);
        await _cache.RemoveAsync("hostels:active", ct); await _cache.RemoveAsync("hostels:all", ct);
        return Result<HostelDto>.Ok(new HostelDto(h.Id, h.Name, h.Description, h.Address, h.IsActive, h.TotalCapacity, h.TotalOccupancy, h.AvailableSpots));
    }
}
