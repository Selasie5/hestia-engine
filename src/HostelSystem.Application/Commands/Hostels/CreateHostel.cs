using HostelSystem.Application.Common;
using HostelSystem.Application.DTOs;
using HostelSystem.Application.Interfaces;
using HostelSystem.Domain.Entities;
using MediatR;

namespace HostelSystem.Application.Commands.Hostels;

public record CreateHostelCommand(string Name, string Address, string? Description) : IRequest<Result<HostelDto>>;

public class CreateHostelHandler : IRequestHandler<CreateHostelCommand, Result<HostelDto>>
{
    private readonly IHostelRepository _hostelRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;

    public CreateHostelHandler(IHostelRepository hostelRepository, IUnitOfWork unitOfWork, ICacheService cacheService)
    {
        _hostelRepository = hostelRepository;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
    }

    public async Task<Result<HostelDto>> Handle(CreateHostelCommand cmd, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.Name) || string.IsNullOrWhiteSpace(cmd.Address))
            return Result<HostelDto>.Fail("Name and Address are required.");

        Hostel hostel;
        try
        {
            hostel = new Hostel(cmd.Name.Trim(), cmd.Address.Trim(), cmd.Description?.Trim());
        }
        catch (Exception ex) { return Result<HostelDto>.Fail(ex.Message); }

        await _hostelRepository.AddAsync(hostel, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        await _cacheService.RemoveAsync("hostels:active", ct);
        await _cacheService.RemoveAsync("hostels:all", ct);

        var dto = new HostelDto(hostel.Id, hostel.Name, hostel.Description, hostel.Address, hostel.IsActive, hostel.TotalCapacity, hostel.TotalOccupancy, hostel.AvailableSpots);
        return Result<HostelDto>.Ok(dto);
    }
}
