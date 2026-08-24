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
    private readonly IPaymentRepository _paymentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;

    public ApproveApplicationHandler(
        IApplicationRepository applicationRepository,
        IRoomRepository roomRepository,
        IStudentRepository studentRepository,
        IAllocationRepository allocationRepository,
        IPaymentRepository paymentRepository,
        IUnitOfWork unitOfWork,
        ICacheService cacheService)
    {
        _applicationRepository = applicationRepository;
        _roomRepository = roomRepository;
        _studentRepository = studentRepository;
        _allocationRepository = allocationRepository;
        _paymentRepository = paymentRepository;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
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

        // Save allocation first to get its Id (needed for Payment FK)
        await _unitOfWork.SaveChangesAsync(ct);

        // Create payment (amount = room price, due in 14 days) per task spec
        var payment = new Domain.Entities.Payment(
            student.Id,
            allocation.Id,
            room.PricePerSemester,
            DateTime.UtcNow.AddDays(14));

        await _paymentRepository.AddAsync(payment, ct);

        // Save payment — concurrency token on Room may still conflict, handled by EF
        await _unitOfWork.SaveChangesAsync(ct);

        // Dispatch domain events AFTER successful save
        await _unitOfWork.DispatchDomainEventsAsync(ct);

        // Room occupancy changed — invalidate cached availability
        await _cacheService.RemoveAsync($"rooms:available:{room.HostelId}", ct);

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
