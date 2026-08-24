using HostelSystem.Application.Common;
using HostelSystem.Application.DTOs;
using HostelSystem.Application.Interfaces;
using MediatR;

namespace HostelSystem.Application.Commands.Allocations;

public record CheckoutAllocationCommand(int AllocationId, string RequestorUserId, bool IsAdmin) : IRequest<Result<AllocationDto>>;

public class CheckoutAllocationHandler : IRequestHandler<CheckoutAllocationCommand, Result<AllocationDto>>
{
    private readonly IAllocationRepository _allocationRepository;
    private readonly IRoomRepository _roomRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;

    public CheckoutAllocationHandler(
        IAllocationRepository allocationRepository,
        IRoomRepository roomRepository,
        IStudentRepository studentRepository,
        IUnitOfWork unitOfWork,
        ICacheService cacheService)
    {
        _allocationRepository = allocationRepository;
        _roomRepository = roomRepository;
        _studentRepository = studentRepository;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
    }

    public async Task<Result<AllocationDto>> Handle(CheckoutAllocationCommand cmd, CancellationToken ct)
    {
        var allocation = await _allocationRepository.GetByIdAsync(cmd.AllocationId, ct);
        if (allocation is null)
            return Result<AllocationDto>.Fail("Allocation not found.");

        if (!allocation.IsActive)
            return Result<AllocationDto>.Fail("Allocation already checked out.");

        // Ownership check: student must own allocation unless admin
        if (!cmd.IsAdmin)
        {
            var student = await _studentRepository.GetByIdAsync(allocation.StudentId, ct);
            if (student is null || student.UserId != cmd.RequestorUserId)
                return Result<AllocationDto>.Fail("Forbidden: you do not own this allocation.");
        }

        var room = await _roomRepository.GetByIdAsync(allocation.RoomId, ct);
        if (room is null)
            return Result<AllocationDto>.Fail("Room not found.");

        try
        {
            allocation.CheckOut();
            room.ReleaseStudent();
        }
        catch (Exception ex)
        {
            return Result<AllocationDto>.Fail(ex.Message);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        await _unitOfWork.DispatchDomainEventsAsync(ct);

        await _cacheService.RemoveAsync($"rooms:available:{room.HostelId}", ct);

        var studentForDto = await _studentRepository.GetByIdAsync(allocation.StudentId, ct);
        return Result<AllocationDto>.Ok(new AllocationDto(
            allocation.Id,
            allocation.StudentId,
            studentForDto?.FullName ?? "N/A",
            room.Id,
            room.RoomNumber,
            room.Hostel?.Name ?? "N/A",
            allocation.AllocationDate,
            allocation.IsActive));
    }
}
