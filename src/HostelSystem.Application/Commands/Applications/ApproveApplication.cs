using MediatR;
using HostelSystem.Application.Common;
using HostelSystem.Application.DTOs;
using HostelSystem.Application.Interfaces;

namespace HostelSystem.Application.Commands.Applications;

// ============================================================
// TEMPLATE for Approve — demonstrates optimistic concurrency handling
// ============================================================

public record ApproveApplicationCommand(
    int ApplicationId,
    string ReviewedBy
) : IRequest<Result<AllocationDto>>;

public class ApproveApplicationHandler : IRequestHandler<ApproveApplicationCommand, Result<AllocationDto>>
{
    private readonly IApplicationRepository _applicationRepository;
    private readonly IRoomRepository _roomRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IAllocationRepository _allocationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ApproveApplicationHandler(
        IApplicationRepository applicationRepository,
        IRoomRepository roomRepository,
        IStudentRepository studentRepository,
        IAllocationRepository allocationRepository,
        IUnitOfWork unitOfWork)
    {
        _applicationRepository = applicationRepository;
        _roomRepository = roomRepository;
        _studentRepository = studentRepository;
        _allocationRepository = allocationRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AllocationDto>> Handle(ApproveApplicationCommand cmd, CancellationToken ct)
    {
        var application = await _applicationRepository.GetByIdAsync(cmd.ApplicationId, ct);
        if (application is null)
            return Result<AllocationDto>.Fail("Application not found.");

        var student = await _studentRepository.GetByIdAsync(application.StudentId, ct);
        if (student is null)
            return Result<AllocationDto>.Fail("Student not found.");

        var room = await _roomRepository.GetByIdAsync(application.RoomId, ct);
        if (room is null)
            return Result<AllocationDto>.Fail("Room not found.");

        // Domain method enforces invariants (throws if not pending, etc.)
        application.Approve(cmd.ReviewedBy);

        // Domain method checks capacity and adjusts occupancy
        room.AllocateStudent(student);

        // Create allocation record
        var allocation = new Domain.Entities.RoomAllocation(
            student.Id, room.Id, application.Id);

        await _allocationRepository.AddAsync(allocation, ct);

        // SaveChanges — if RowVersion conflicts, DbUpdateConcurrencyException is thrown
        await _unitOfWork.SaveChangesAsync(ct);

        // Dispatch domain events AFTER successful save
        await _unitOfWork.DispatchDomainEventsAsync(ct);

        return Result<AllocationDto>.Ok(new AllocationDto(
            allocation.Id,
            student.Id,
            student.FullName,
            room.Id,
            room.RoomNumber,
            room.Hostel?.Name ?? "N/A",
            allocation.AllocationDate,
            allocation.IsActive));
    }
}
