using HostelSystem.Application.Common;
using HostelSystem.Application.Interfaces;
using MediatR;

namespace HostelSystem.Application.Commands.Applications;

public record CancelApplicationCommand(int ApplicationId, string RequestorUserId, bool IsAdmin = false, string? Reason = null) : IRequest<Result<bool>>;

public class CancelApplicationHandler : IRequestHandler<CancelApplicationCommand, Result<bool>>
{
    private readonly IApplicationRepository _applicationRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IAllocationRepository _allocationRepository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CancelApplicationHandler(
        IApplicationRepository applicationRepository,
        IStudentRepository studentRepository,
        IAllocationRepository allocationRepository,
        IPaymentRepository paymentRepository,
        IUnitOfWork unitOfWork)
    {
        _applicationRepository = applicationRepository;
        _studentRepository = studentRepository;
        _allocationRepository = allocationRepository;
        _paymentRepository = paymentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<bool>> Handle(CancelApplicationCommand cmd, CancellationToken ct)
    {
        var app = await _applicationRepository.GetByIdAsync(cmd.ApplicationId, ct);
        if (app is null)
            return Result<bool>.Fail("Application not found.");

        // Verify ownership unless caller is admin — handler itself doesn't know role, controller enforces admin bypass via separate path.
        // Here we check: if app.Student.UserId != RequestorUserId, fail. Caller must resolve Student.
        var student = await _studentRepository.GetByIdAsync(app.StudentId, ct);
        if (student is null)
            return Result<bool>.Fail("Student not found.");

        var isOwner = student.UserId == cmd.RequestorUserId;
        if (!isOwner && !cmd.IsAdmin)
            return Result<bool>.Fail("Forbidden: you do not own this application.");

        try
        {
            app.Cancel(cmd.Reason);
        }
        catch (Exception ex)
        {
            return Result<bool>.Fail(ex.Message);
        }

        // Refund if there's a completed payment for the related allocation
        var allocation = await _allocationRepository.GetByApplicationIdAsync(app.Id, ct);
        if (allocation is not null)
        {
            var payment = await _paymentRepository.GetByAllocationIdAsync(allocation.Id, ct);
            if (payment is not null && payment.Status == Domain.Enums.PaymentStatus.Completed)
            {
                try { payment.MarkRefunded(); }
                catch (Exception ex) { return Result<bool>.Fail($"Application cancelled but refund failed: {ex.Message}"); }
            }
        }

        await _unitOfWork.SaveChangesAsync(ct);
        await _unitOfWork.DispatchDomainEventsAsync(ct);
        return Result<bool>.Ok(true);
    }
}
