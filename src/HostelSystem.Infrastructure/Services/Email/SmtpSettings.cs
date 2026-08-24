namespace HostelSystem.Infrastructure.Services.Email;

public class SmtpSettings
{
    public const string SectionName = "Smtp";
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public string? User { get; set; }
    public string? Password { get; set; }
    public string From { get; set; } = "noreply@hostelsystem.local";
    public string FromName { get; set; } = "HostelSystem";
    public bool EnableSsl { get; set; } = true;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(User);
}
