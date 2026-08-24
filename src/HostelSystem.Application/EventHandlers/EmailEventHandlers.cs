using HostelSystem.Application.Interfaces;
using HostelSystem.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HostelSystem.Application.EventHandlers;

public class ApplicationApprovedEmailHandler : INotificationHandler<ApplicationApprovedEvent>
{
    private readonly IStudentRepository _students;
    private readonly IRoomRepository _rooms;
    private readonly IEmailService _email;
    private readonly ILogger<ApplicationApprovedEmailHandler> _logger;
    public ApplicationApprovedEmailHandler(IStudentRepository students, IRoomRepository rooms, IEmailService email, ILogger<ApplicationApprovedEmailHandler> logger)
    { _students = students; _rooms = rooms; _email = email; _logger = logger; }

    public async Task Handle(ApplicationApprovedEvent n, CancellationToken ct)
    {
        try
        {
            var s = await _students.GetByIdAsync(n.StudentId, ct);
            var r = await _rooms.GetByIdAsync(n.RoomId, ct);
            // Email is on Identity user; we use studentNumber@placeholder if no email found — controller owns email, here we try studentNumber fallback
            var email = $"{s?.StudentNumber}@placeholder.local";
            // If we can resolve via student FullName, send
            if (s is not null)
                await _email.SendApplicationStatusEmailAsync(email, s.FullName, "Approved", r?.RoomNumber, ct);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed approved email for app {Id}", n.ApplicationId); }
    }
}

public class ApplicationRejectedEmailHandler : INotificationHandler<ApplicationRejectedEvent>
{
    private readonly IStudentRepository _students;
    private readonly IRoomRepository _rooms;
    private readonly IEmailService _email;
    private readonly ILogger<ApplicationRejectedEmailHandler> _logger;
    public ApplicationRejectedEmailHandler(IStudentRepository students, IRoomRepository rooms, IEmailService email, ILogger<ApplicationRejectedEmailHandler> logger)
    { _students = students; _rooms = rooms; _email = email; _logger = logger; }
    public async Task Handle(ApplicationRejectedEvent n, CancellationToken ct)
    {
        try
        {
            var s = await _students.GetByIdAsync(n.StudentId, ct);
            if (s is not null)
                await _email.SendApplicationStatusEmailAsync($"{s.StudentNumber}@placeholder.local", s.FullName, "Rejected", null, ct);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed rejected email for app {Id}", n.ApplicationId); }
    }
}

public class PaymentCompletedEmailHandler : INotificationHandler<PaymentCompletedEvent>
{
    private readonly IPaymentRepository _payments;
    private readonly IStudentRepository _students;
    private readonly IEmailService _email;
    private readonly ILogger<PaymentCompletedEmailHandler> _logger;
    public PaymentCompletedEmailHandler(IPaymentRepository payments, IStudentRepository students, IEmailService email, ILogger<PaymentCompletedEmailHandler> logger)
    { _payments = payments; _students = students; _email = email; _logger = logger; }
    public async Task Handle(PaymentCompletedEvent n, CancellationToken ct)
    {
        try
        {
            var p = await _payments.GetByIdAsync(n.PaymentId, ct);
            if (p is null) return;
            var s = await _students.GetByIdAsync(p.StudentId, ct);
            if (s is not null)
                await _email.SendPaymentConfirmationAsync($"{s.StudentNumber}@placeholder.local", s.FullName, n.Amount, p.AllocationId.ToString(), ct);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed payment email {Id}", n.PaymentId); }
    }
}
