namespace HostelSystem.Infrastructure.Services.Payments;

public class PaystackSettings
{
    public const string SectionName = "Paystack";

    /// <summary>Secret key — from env var / user-secrets Paystack:SecretKey — never committed.</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Public key — safe to expose to client via config if needed.</summary>
    public string PublicKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.paystack.co";

    /// <summary>Currency for Paystack — Ghana uses GHS; test defaults to GHS. NGN also valid.</summary>
    public string Currency { get; set; } = "GHS";

    /// <summary>Optional callback URL after payment — can be overridden per request.</summary>
    public string? CallbackUrl { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(SecretKey);
}
