using HostelSystem.Application.Common;
using HostelSystem.Application.Interfaces;
using MediatR;

namespace HostelSystem.Application.Commands.Payments;

public record VerifyPaymentCommand(string TransactionReference) : IRequest<Result<VerifyPaymentResult>>;

public record VerifyPaymentResult(
    string TransactionReference,
    decimal Amount,
    string Status,
    DateTime? PaidOn,
    string? PaymentMethod);

public class VerifyPaymentHandler : IRequestHandler<VerifyPaymentCommand, Result<VerifyPaymentResult>>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IPaymentGateway _gateway;
    private readonly IUnitOfWork _unitOfWork;

    public VerifyPaymentHandler(IPaymentRepository paymentRepository, IPaymentGateway gateway, IUnitOfWork unitOfWork)
    {
        _paymentRepository = paymentRepository;
        _gateway = gateway;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<VerifyPaymentResult>> Handle(VerifyPaymentCommand cmd, CancellationToken ct)
    {
        var payment = await _paymentRepository.GetByTransactionReferenceAsync(cmd.TransactionReference, ct);
        if (payment is null)
            return Result<VerifyPaymentResult>.Fail("Payment not found for reference.");

        if (payment.Status == Domain.Enums.PaymentStatus.Completed)
        {
            return Result<VerifyPaymentResult>.Ok(new VerifyPaymentResult(
                payment.TransactionReference!,
                payment.Amount,
                payment.Status.ToString(),
                payment.PaidOn,
                payment.PaymentMethod));
        }

        var verifyResult = await _gateway.VerifyPaymentAsync(cmd.TransactionReference, ct);
        if (!verifyResult.IsSuccess)
            return Result<VerifyPaymentResult>.Fail(verifyResult.ErrorMessage ?? "Verification failed.");

        // Gateway confirmed — mark completed if still pending/failed
        if (payment.Status == Domain.Enums.PaymentStatus.Pending || payment.Status == Domain.Enums.PaymentStatus.Failed)
        {
            try
            {
                payment.MarkCompleted(cmd.TransactionReference, payment.PaymentMethod ?? "Paystack");
                await _unitOfWork.SaveChangesAsync(ct);
                await _unitOfWork.DispatchDomainEventsAsync(ct);
            }
            catch (Exception ex)
            {
                return Result<VerifyPaymentResult>.Fail(ex.Message);
            }
        }

        return Result<VerifyPaymentResult>.Ok(new VerifyPaymentResult(
            payment.TransactionReference!,
            payment.Amount,
            payment.Status.ToString(),
            payment.PaidOn,
            payment.PaymentMethod));
    }
}
