using HostelSystem.Domain.Events;

namespace HostelSystem.Application.Interfaces;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task DispatchDomainEventsAsync(CancellationToken ct = default);
}

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default) where T : class;
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task RemoveByPatternAsync(string pattern, CancellationToken ct = default);
}

public interface IPaymentGateway
{
    Task<PaymentGatewayResult> InitializePaymentAsync(decimal amount, string email, string reference, CancellationToken ct = default);
    Task<PaymentGatewayResult> VerifyPaymentAsync(string reference, CancellationToken ct = default);
}

/// <summary>
/// Result from Paystack (or other) gateway. AuthorizationUrl is populated on Initialize only.
/// </summary>
public record PaymentGatewayResult(
    bool IsSuccess,
    string? TransactionReference,
    string? AuthorizationUrl = null,
    string? AccessCode = null,
    string? ErrorMessage = null);

public interface IQrCodeService
{
    /// <summary>Generates a PNG byte array for the given content.</summary>
    byte[] GeneratePng(string content, int pixelsPerModule = 20);
    /// <summary>Generates an SVG string for the given content.</summary>
    string GenerateSvg(string content, int pixelsPerModule = 20);
}

public interface IEmailService
{
    Task SendApplicationStatusEmailAsync(string to, string studentName, string status, string? roomNumber = null, CancellationToken ct = default);
    Task SendPaymentConfirmationAsync(string to, string studentName, decimal amount, string roomNumber, CancellationToken ct = default);
}

public interface IDomainEventDispatcher
{
    Task DispatchAsync(IDomainEvent domainEvent, CancellationToken ct = default);
}
