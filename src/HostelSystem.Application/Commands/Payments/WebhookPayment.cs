using HostelSystem.Application.Common;
using HostelSystem.Application.Interfaces;
using MediatR;

namespace HostelSystem.Application.Commands.Payments;

/// <summary>
/// Called from PaymentsController webhook after signature verification.
/// </summary>
public record ConfirmPaymentCommand(string TransactionReference, string PaymentMethod = "Paystack") : IRequest<Result<ConfirmPaymentResult>>;

public record ConfirmPaymentResult(string TransactionReference, decimal Amount, string Status);

public class ConfirmPaymentHandler : IRequestHandler<ConfirmPaymentCommand, Result<ConfirmPaymentResult>>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ConfirmPaymentHandler(IPaymentRepository paymentRepository, IUnitOfWork unitOfWork)
    {
        _paymentRepository = paymentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ConfirmPaymentResult>> Handle(ConfirmPaymentCommand cmd, CancellationToken ct)
    {
        var payment = await _paymentRepository.GetByTransactionReferenceAsync(cmd.TransactionReference, ct);
        if (payment is null)
            return Result<ConfirmPaymentResult>.Fail($"Payment not found for reference {cmd.TransactionReference}.");

        if (payment.Status == Domain.Enums.PaymentStatus.Completed)
            return Result<ConfirmPaymentResult>.Ok(new ConfirmPaymentResult(payment.TransactionReference!, payment.Amount, payment.Status.ToString()));

        if (payment.Status == Domain.Enums.PaymentStatus.Refunded)
            return Result<ConfirmPaymentResult>.Fail("Cannot confirm a refunded payment.");

        try
        {
            payment.MarkCompleted(cmd.TransactionReference, cmd.PaymentMethod);
            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.DispatchDomainEventsAsync(ct);
        }
        catch (Exception ex)
        {
            return Result<ConfirmPaymentResult>.Fail(ex.Message);
        }

        return Result<ConfirmPaymentResult>.Ok(new ConfirmPaymentResult(payment.TransactionReference!, payment.Amount, payment.Status.ToString()));
    }
}
