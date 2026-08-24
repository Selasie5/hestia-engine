using HostelSystem.Application.Common;
using HostelSystem.Application.Interfaces;
using MediatR;

namespace HostelSystem.Application.Commands.Payments;

public record RefundPaymentCommand(int PaymentId) : IRequest<Result<RefundPaymentResult>>;
public record CancelApplicationWithRefundCommand(int ApplicationId, string? Reason = null) : IRequest<Result<RefundPaymentResult>>;

public record RefundPaymentResult(int PaymentId, string Status);

public class RefundPaymentHandler : IRequestHandler<RefundPaymentCommand, Result<RefundPaymentResult>>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RefundPaymentHandler(IPaymentRepository paymentRepository, IUnitOfWork unitOfWork)
    {
        _paymentRepository = paymentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<RefundPaymentResult>> Handle(RefundPaymentCommand cmd, CancellationToken ct)
    {
        var payment = await _paymentRepository.GetByIdAsync(cmd.PaymentId, ct);
        if (payment is null)
            return Result<RefundPaymentResult>.Fail("Payment not found.");

        try
        {
            payment.MarkRefunded();
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            return Result<RefundPaymentResult>.Fail(ex.Message);
        }

        return Result<RefundPaymentResult>.Ok(new RefundPaymentResult(payment.Id, payment.Status.ToString()));
    }
}

public class CancelApplicationWithRefundHandler : IRequestHandler<CancelApplicationWithRefundCommand, Result<RefundPaymentResult>>
{
    private readonly IApplicationRepository _applicationRepository;
    private readonly IAllocationRepository _allocationRepository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CancelApplicationWithRefundHandler(
        IApplicationRepository applicationRepository,
        IAllocationRepository allocationRepository,
        IPaymentRepository paymentRepository,
        IUnitOfWork unitOfWork)
    {
        _applicationRepository = applicationRepository;
        _allocationRepository = allocationRepository;
        _paymentRepository = paymentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<RefundPaymentResult>> Handle(CancelApplicationWithRefundCommand cmd, CancellationToken ct)
    {
        var application = await _applicationRepository.GetByIdAsync(cmd.ApplicationId, ct);
        if (application is null)
            return Result<RefundPaymentResult>.Fail("Application not found.");

        try
        {
            application.Cancel(cmd.Reason);
        }
        catch (Exception ex)
        {
            return Result<RefundPaymentResult>.Fail(ex.Message);
        }

        // Find allocation linked to this application and refund its payment if completed
        var allocation = await _allocationRepository.GetByApplicationIdAsync(application.Id, ct);
        if (allocation is not null)
        {
            var payment = await _paymentRepository.GetByAllocationIdAsync(allocation.Id, ct);
            if (payment is not null && payment.Status == Domain.Enums.PaymentStatus.Completed)
            {
                try
                {
                    payment.MarkRefunded();
                }
                catch (Exception ex)
                {
                    return Result<RefundPaymentResult>.Fail($"Application cancelled but refund failed: {ex.Message}");
                }
                await _unitOfWork.SaveChangesAsync(ct);
                return Result<RefundPaymentResult>.Ok(new RefundPaymentResult(payment.Id, payment.Status.ToString()));
            }
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return Result<RefundPaymentResult>.Ok(new RefundPaymentResult(0, "Cancelled"));
    }
}
