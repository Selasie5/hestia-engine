# Email Setup — HostelSystem

`IEmailService` is resolved from `SmtpSettings` (`Smtp:`). If `Smtp:Host` is empty, the service logs to console/Serilog (no SMTP).

## Dev — Console fallback (default)

No config needed. Emails appear in `Logs/hostel-system-.log` and console as `[Email:ConsoleFallback]`.

## Dev — Mailtrap / Ethereal

```bash
dotnet user-secrets set "Smtp:Host" "smtp.mailtrap.io" --project src/HostelSystem.Api/HostelSystem.Api.csproj
dotnet user-secrets set "Smtp:Port" "2525" --project src/HostelSystem.Api/HostelSystem.Api.csproj
dotnet user-secrets set "Smtp:User" "your-mailtrap-user" --project src/HostelSystem.Api/HostelSystem.Api.csproj
dotnet user-secrets set "Smtp:Password" "your-mailtrap-pass" --project src/HostelSystem.Api/HostelSystem.Api.csproj
dotnet user-secrets set "Smtp:From" "noreply@hostelsystem.local" --project src/HostelSystem.Api/HostelSystem.Api.csproj
```

Or via env: `Smtp__Host`, `Smtp__Port`, etc. (see `.env.example`).

## Events wired

- `ApplicationApprovedEvent` → `ApplicationApprovedEmailHandler` → `SendApplicationStatusEmailAsync` (Approved)
- `ApplicationRejectedEvent` → `ApplicationRejectedEmailHandler`
- `PaymentCompletedEvent` → `PaymentCompletedEmailHandler` → `SendPaymentConfirmationAsync`

All handlers are `INotificationHandler<T>` discovered via `AddMediatR(Assembly.GetExecutingAssembly())`. If email fails, the handler logs warning but does not break the domain transaction.

## Testing

1. Trigger an approval: `POST /api/v1.0/Applications/1/approve` as Admin.
2. Check logs: `Logs/hostel-system-*.log` or console.
3. With Mailtrap, check inbox.

## Production

Use a managed SMTP (SendGrid, SES) with TLS. Store creds in Key Vault / env. Do not commit secrets.
