using HostelSystem.Application.Common;
using HostelSystem.Application.Interfaces;
using MediatR;

namespace HostelSystem.Application.Commands.Payments;

public record InitiatePaymentCommand(int PaymentId, string Email, string? CallbackUrl = null) : IRequest<Result<InitiatePaymentResult>>;

public record InitiatePaymentResult(
    string TransactionReference,
    decimal Amount,
    string? AuthorizationUrl,
    string? AccessCode,
    string Status);

public class InitiatePaymentHandler : IRequestHandler<InitiatePaymentCommand, Result<InitiatePaymentResult>>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IPaymentGateway _gateway;
    private readonly IUnitOfWork _unitOfWork;

    public InitiatePaymentHandler(IPaymentRepository paymentRepository, IPaymentGateway gateway, IUnitOfWork unitOfWork)
    {
        _paymentRepository = paymentRepository;
        _gateway = gateway;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<InitiatePaymentResult>> Handle(InitiatePaymentCommand cmd, CancellationToken ct)
    {
        var payment = await _paymentRepository.GetByIdAsync(cmd.PaymentId, ct);
        if (payment is null)
            return Result<InitiatePaymentResult>.Fail("Payment not found.");

        if (payment.Status == Domain.Enums.PaymentStatus.Completed)
            return Result<InitiatePaymentResult>.Fail("Payment already completed.");

        if (payment.Status == Domain.Enums.PaymentStatus.Refunded)
            return Result<InitiatePaymentResult>.Fail("Cannot initiate refunded payment.");

        // Generate reference if not yet assigned — deterministic prefix HS-
        var reference = payment.TransactionReference;
        if (string.IsNullOrWhiteSpace(reference))
        {
            reference = $"HS-{payment.Id}-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
            try
            {
                payment.AssignReference(reference, "Paystack");
            }
            catch (Exception ex)
            {
                return Result<InitiatePaymentResult>.Fail(ex.Message);
            }
        }

        var gatewayResult = await _gateway.InitializePaymentAsync(payment.Amount, cmd.Email, reference, ct);

        if (!gatewayResult.IsSuccess)
        {
            // Keep the assigned reference for polling even if gateway fails — payment stays pending
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<InitiatePaymentResult>.Fail(gatewayResult.ErrorMessage ?? "Failed to initialize payment with gateway.");
        }

        // Ensure TransactionReference matches gateway's reference (should be same as we sent)
        var finalRef = gatewayResult.TransactionReference ?? reference;
        if (finalRef != payment.TransactionReference)
        {
            // If gateway returned different reference, re-assign (only if still pending)
            // For safety, don't overwrite; just ensure stored reference matches returned
            // If it differs, we already have a reference assigned — log and keep original
        }

        await _unitOfWork.SaveChangesAsync(ct);

        return Result<InitiatePaymentResult>.Ok(new InitiatePaymentResult(
            finalRef,
            payment.Amount,
            gatewayResult.AuthorizationUrl,
            gatewayResult.AccessCode,
            payment.Status.ToString()));
    }
}
