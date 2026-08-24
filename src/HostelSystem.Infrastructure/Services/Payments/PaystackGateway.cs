using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using HostelSystem.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HostelSystem.Infrastructure.Services.Payments;

public class PaystackGateway : IPaymentGateway
{
    private readonly HttpClient _httpClient;
    private readonly PaystackSettings _settings;
    private readonly ILogger<PaystackGateway> _logger;

    public PaystackGateway(HttpClient httpClient, IOptions<PaystackSettings> options, ILogger<PaystackGateway> logger)
    {
        _httpClient = httpClient;
        _settings = options.Value;
        _logger = logger;

        // Configure base address / auth header per request; also set defaults here
        if (_httpClient.BaseAddress is null && !string.IsNullOrWhiteSpace(_settings.BaseUrl))
            _httpClient.BaseAddress = new Uri(_settings.BaseUrl.TrimEnd('/') + "/");

        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<PaymentGatewayResult> InitializePaymentAsync(decimal amount, string email, string reference, CancellationToken ct = default)
    {
        if (!_settings.IsConfigured)
        {
            _logger.LogWarning("Paystack SecretKey not configured — Paystack:SecretKey missing");
            return new PaymentGatewayResult(false, reference, ErrorMessage: "Payment gateway not configured.");
        }

        if (string.IsNullOrWhiteSpace(email))
            return new PaymentGatewayResult(false, reference, ErrorMessage: "Email is required for Paystack.");
        if (string.IsNullOrWhiteSpace(reference))
            return new PaymentGatewayResult(false, null, ErrorMessage: "Transaction reference is required.");

        // Paystack expects amount in kobo/pesewa (smallest unit)
        var amountInKobo = (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);

        var payload = new
        {
            amount = amountInKobo,
            email,
            reference,
            currency = _settings.Currency,
            callback_url = _settings.CallbackUrl
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "transaction/initialize")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.SecretKey);

        _logger.LogInformation("Initializing Paystack payment {Reference} for {Email} amount {Amount} ({Kobo} kobo)", reference, email, amount, amountInKobo);

        try
        {
            var response = await _httpClient.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Paystack initialize failed {Status} {Body}", response.StatusCode, body);
                var err = TryExtractMessage(body) ?? $"Paystack error: {response.StatusCode}";
                return new PaymentGatewayResult(false, reference, ErrorMessage: err);
            }

            var result = JsonSerializer.Deserialize<PaystackInitializeResponse>(body, JsonOptions);
            if (result is null || result.Status != true || result.Data is null)
            {
                _logger.LogError("Paystack initialize unexpected payload {Body}", body);
                return new PaymentGatewayResult(false, reference, ErrorMessage: result?.Message ?? "Failed to initialize payment.");
            }

            return new PaymentGatewayResult(
                IsSuccess: true,
                TransactionReference: reference,
                AuthorizationUrl: result.Data.AuthorizationUrl,
                AccessCode: result.Data.AccessCode,
                ErrorMessage: null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during Paystack initialize {Reference}", reference);
            return new PaymentGatewayResult(false, reference, ErrorMessage: ex.Message);
        }
    }

    public async Task<PaymentGatewayResult> VerifyPaymentAsync(string reference, CancellationToken ct = default)
    {
        if (!_settings.IsConfigured)
            return new PaymentGatewayResult(false, reference, ErrorMessage: "Payment gateway not configured.");
        if (string.IsNullOrWhiteSpace(reference))
            return new PaymentGatewayResult(false, null, ErrorMessage: "Reference is required.");

        var request = new HttpRequestMessage(HttpMethod.Get, $"transaction/verify/{reference}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.SecretKey);

        _logger.LogInformation("Verifying Paystack payment {Reference}", reference);

        try
        {
            var response = await _httpClient.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Paystack verify failed {Status} {Body}", response.StatusCode, body);
                var err = TryExtractMessage(body) ?? $"Verify failed: {response.StatusCode}";
                return new PaymentGatewayResult(false, reference, ErrorMessage: err);
            }

            var result = JsonSerializer.Deserialize<PaystackVerifyResponse>(body, JsonOptions);
            if (result is null || result.Status != true || result.Data is null)
                return new PaymentGatewayResult(false, reference, ErrorMessage: result?.Message ?? "Verification failed.");

            // Paystack status: success, failed, abandoned, etc. — also check gateway response/paid_at
            var isSuccess = result.Data.Status.Equals("success", StringComparison.OrdinalIgnoreCase)
                            && result.Data.GatewayResponse?.Contains("success", StringComparison.OrdinalIgnoreCase) != false
                            || result.Data.Status.Equals("success", StringComparison.OrdinalIgnoreCase);

            // More strict: data.status must be success
            isSuccess = result.Data.Status.Equals("success", StringComparison.OrdinalIgnoreCase);

            if (!isSuccess)
                return new PaymentGatewayResult(false, reference, ErrorMessage: $"Payment not successful: {result.Data.GatewayResponse ?? result.Data.Status}");

            return new PaymentGatewayResult(true, reference, ErrorMessage: null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during Paystack verify {Reference}", reference);
            return new PaymentGatewayResult(false, reference, ErrorMessage: ex.Message);
        }
    }

    private static string? TryExtractMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("message", out var msg))
                return msg.GetString();
        }
        catch { }
        return null;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    // Paystack DTOs — minimal

    private record PaystackInitializeResponse(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("data")] PaystackInitData? Data);

    private record PaystackInitData(
        [property: JsonPropertyName("authorization_url")] string AuthorizationUrl,
        [property: JsonPropertyName("access_code")] string AccessCode,
        [property: JsonPropertyName("reference")] string Reference);

    private record PaystackVerifyResponse(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("data")] PaystackVerifyData? Data);

    private record PaystackVerifyData(
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("reference")] string Reference,
        [property: JsonPropertyName("gateway_response")] string? GatewayResponse,
        [property: JsonPropertyName("amount")] long Amount,
        [property: JsonPropertyName("currency")] string? Currency,
        [property: JsonPropertyName("paid_at")] string? PaidAt);
}
