using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HostelSystem.Application.Commands.Payments;
using HostelSystem.Application.Interfaces;
using HostelSystem.Infrastructure.Services.Payments;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace HostelSystem.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
public class PaymentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IQrCodeService _qrCodeService;
    private readonly PaystackSettings _paystackSettings;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        IMediator mediator,
        IPaymentRepository paymentRepository,
        IStudentRepository studentRepository,
        IQrCodeService qrCodeService,
        IOptions<PaystackSettings> paystackOptions,
        ILogger<PaymentsController> logger)
    {
        _mediator = mediator;
        _paymentRepository = paymentRepository;
        _studentRepository = studentRepository;
        _qrCodeService = qrCodeService;
        _paystackSettings = paystackOptions.Value;
        _logger = logger;
    }

    public record InitiateRequest(int PaymentId, string Email, string? CallbackUrl = null);
    public record RefundRequest(int PaymentId);

    /// <summary>
    /// List my payments (Student). Admin can view any via admin endpoint.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetMy(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized(new { error = "Invalid token." });
        var student = await _studentRepository.GetByUserIdAsync(userId, ct);
        if (student is null) return NotFound(new { error = "Student profile not found." });
        var payments = await _paymentRepository.GetByStudentIdAsync(student.Id, ct);
        var dtos = payments.Select(p => new { p.Id, p.Amount, Status = p.Status.ToString(), p.TransactionReference, p.PaidOn, p.DueDate, p.IsOverdue, p.PaymentMethod, p.AllocationId });
        return Ok(dtos);
    }

    /// <summary>
    /// Initiate a payment — returns Paystack authorization URL. Verifies Student owns the payment.
    /// </summary>
    [HttpPost("initiate")]
    [Authorize]
    public async Task<IActionResult> Initiate([FromBody] InitiateRequest request, CancellationToken ct)
    {
        if (request.PaymentId <= 0 || string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { error = "PaymentId and Email are required." });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        var isAdmin = User.IsInRole("Admin");
        if (!isAdmin && !string.IsNullOrWhiteSpace(userId))
        {
            var student = await _studentRepository.GetByUserIdAsync(userId, ct);
            var pay = await _paymentRepository.GetByIdAsync(request.PaymentId, ct);
            if (student is null || pay is null || pay.StudentId != student.Id)
                return Forbid();
        }

        var cmd = new InitiatePaymentCommand(request.PaymentId, request.Email, request.CallbackUrl);
        var result = await _mediator.Send(cmd, ct);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });

        return Ok(new
        {
            transactionReference = result.Value!.TransactionReference,
            amount = result.Value.Amount,
            authorizationUrl = result.Value.AuthorizationUrl,
            accessCode = result.Value.AccessCode,
            status = result.Value.Status
        });
    }

    /// <summary>
    /// Paystack webhook — verifies signature and marks payment completed.
    /// </summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {
        // Read raw body for signature verification
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync();
        Request.Body.Position = 0;

        var signature = Request.Headers["x-paystack-signature"].FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(_paystackSettings.SecretKey) && !string.IsNullOrWhiteSpace(signature))
        {
            var computed = ComputeHmacSha512(rawBody, _paystackSettings.SecretKey);
            if (!computed.Equals(signature, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Paystack webhook signature mismatch");
                return Unauthorized(new { error = "Invalid signature" });
            }
        }
        else if (!string.IsNullOrWhiteSpace(_paystackSettings.SecretKey))
        {
            _logger.LogWarning("Paystack webhook missing signature header");
            return Unauthorized(new { error = "Missing signature" });
        }
        // If SecretKey not configured, allow in dev for testing (log warning)
        if (string.IsNullOrWhiteSpace(_paystackSettings.SecretKey))
            _logger.LogWarning("Paystack SecretKey not configured — skipping signature check (dev only)");

        // Parse Paystack event
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(rawBody);
            var root = doc.RootElement;

            var evt = root.TryGetProperty("event", out var e) ? e.GetString() : null;
            var data = root.TryGetProperty("data", out var d) ? d : (System.Text.Json.JsonElement?)null;
            string? reference = null;
            string? status = null;

            if (data.HasValue)
            {
                if (data.Value.TryGetProperty("reference", out var r)) reference = r.GetString();
                if (data.Value.TryGetProperty("status", out var s)) status = s.GetString();
            }

            _logger.LogInformation("Paystack webhook event {Event} ref {Ref} status {Status}", evt, reference, status);

            // Only act on success
            if (evt == "charge.success" && !string.IsNullOrWhiteSpace(reference) && status == "success")
            {
                var paymentMethod = "Paystack";
                // Detect QR/channel if metadata says QR
                if (data.HasValue && data.Value.TryGetProperty("channel", out var ch) && ch.GetString() == "qr")
                    paymentMethod = "QR";

                var result = await _mediator.Send(new ConfirmPaymentCommand(reference, paymentMethod), ct);
                if (!result.IsSuccess)
                    _logger.LogWarning("Webhook confirm failed {Error}", result.Error);
            }

            return Ok(new { received = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process Paystack webhook");
            return BadRequest(new { error = "Invalid payload" });
        }
    }

    /// <summary>
    /// Polling fallback — verify via Paystack API.
    /// </summary>
    [HttpGet("{reference}/verify")]
    [Authorize]
    public async Task<IActionResult> Verify(string reference, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return BadRequest(new { error = "Reference is required." });

        var result = await _mediator.Send(new VerifyPaymentCommand(reference), ct);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });

        return Ok(result.Value);
    }

    /// <summary>
    /// QR code for a payment — encodes payment reference + amount.
    /// Supports ?format=png (default) or svg. Optional ?includeAmount=true.
    /// </summary>
    [HttpGet("{reference}/qr")]
    [Authorize]
    public async Task<IActionResult> GetQr(string reference, [FromQuery] string format = "png", CancellationToken ct = default)
    {
        var payment = await _paymentRepository.GetByTransactionReferenceAsync(reference, ct);
        if (payment is null)
            return NotFound(new { error = "Payment not found." });

        // Encode reference + amount as JSON for scannability; QR readers can parse
        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            reference = payment.TransactionReference,
            amount = payment.Amount,
            currency = _paystackSettings.Currency ?? "GHS",
            status = payment.Status.ToString(),
            paymentId = payment.Id
        });

        if (format.Equals("svg", StringComparison.OrdinalIgnoreCase))
        {
            var svg = _qrCodeService.GenerateSvg(payload);
            return Content(svg, "image/svg+xml");
        }
        else
        {
            var png = _qrCodeService.GeneratePng(payload);
            return File(png, "image/png", $"{reference}.png");
        }
    }

    /// <summary>
    /// Cancel application and refund if completed. Populates PaymentMethod via domain.
    /// </summary>
    [HttpPost("applications/{applicationId:int}/cancel")]
    [Authorize]
    public async Task<IActionResult> CancelWithRefund(int applicationId, [FromBody] CancelRequest? body, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new CancelApplicationWithRefundCommand(applicationId, body?.Reason), ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    public record CancelRequest(string? Reason);

    /// <summary>
    /// Ad-hoc refund by payment id.
    /// </summary>
    [HttpPost("refund")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Refund([FromBody] RefundRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new RefundPaymentCommand(request.PaymentId), ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    private static string ComputeHmacSha512(string data, string secret)
    {
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}
