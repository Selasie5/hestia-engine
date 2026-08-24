using System.Net;
using System.Net.Mail;
using HostelSystem.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HostelSystem.Infrastructure.Services.Email;

public class EmailService : IEmailService
{
    private readonly SmtpSettings _settings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<SmtpSettings> options, ILogger<EmailService> logger)
    {
        _settings = options.Value;
        _logger = logger;
    }

    public async Task SendApplicationStatusEmailAsync(string to, string studentName, string status, string? roomNumber = null, CancellationToken ct = default)
    {
        var subject = status.Equals("Approved", StringComparison.OrdinalIgnoreCase)
            ? $"Your hostel application is approved — Room {roomNumber ?? ""}"
            : $"Hostel application update: {status}";
        var body = status.Equals("Approved", StringComparison.OrdinalIgnoreCase)
            ? $"Hi {studentName},\n\nYour application for room {roomNumber ?? "N/A"} has been APPROVED.\nPlease proceed to payment within 14 days.\n\nHostelSystem"
            : $"Hi {studentName},\n\nYour hostel application status is now: {status}.\nRoom: {roomNumber ?? "N/A"}\n\nHostelSystem";
        await SendAsync(to, subject, body, ct);
    }

    public async Task SendPaymentConfirmationAsync(string to, string studentName, decimal amount, string roomNumber, CancellationToken ct = default)
    {
        var subject = $"Payment confirmed — GHS {amount} for Room {roomNumber}";
        var body = $"Hi {studentName},\n\nYour payment of GHS {amount} for room {roomNumber} was confirmed.\n\nThank you.\nHostelSystem";
        await SendAsync(to, subject, body, ct);
    }

    private async Task SendAsync(string to, string subject, string body, CancellationToken ct)
    {
        if (!_settings.IsConfigured)
        {
            _logger.LogInformation("[Email:ConsoleFallback] To:{To} Subject:{Subject} Body:{Body}", to, subject, body);
            return;
        }

        try
        {
            using var client = new SmtpClient(_settings.Host, _settings.Port)
            {
                Credentials = new NetworkCredential(_settings.User, _settings.Password),
                EnableSsl = _settings.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };
            using var msg = new MailMessage
            {
                From = new MailAddress(_settings.From, _settings.FromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };
            msg.To.Add(to);
            await client.SendMailAsync(msg, ct);
            _logger.LogInformation("Email sent to {To} subject {Subject}", to, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}", to);
            // Swallow — don't break domain flow. Fallback logged.
        }
    }
}
