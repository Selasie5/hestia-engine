# Hestia Engine — Hostel Room Allocation System

Clean Architecture + CQRS/MediatR + Blazor Server — hostel browsing, application workflow (submit → approve/reject/cancel), allocation & checkout, Paystack payments + QR, role-based portals (Student/Admin).

**Stack:** .NET 10, ASP.NET Core API (JWT + Identity), EF Core (SQLite dev / SQL Server prod pluggable via `DatabaseProvider`), Blazor Server (`HostelSystem.Web`), MediatR, FluentValidation, QRCoder, Serilog, Docker Compose, GitHub Actions CI.

## Quick Start ( < 10 min )

```bash
git clone https://github.com/Selasie5/hestia-engine.git
cd hestia-engine

# API — SQLite auto-migrates + seeds on first run (port 8080, Swagger /swagger)
dotnet run --project src/HostelSystem.Api/HostelSystem.Api.csproj
# → http://localhost:8080/health
# → http://localhost:8080/swagger

# Web — Blazor Server (port 5000, proxies to ApiBaseUrl)
dotnet run --project src/HostelSystem.Web/HostelSystem.Web.csproj
# → http://localhost:5000  (login /register → hostels → apply → pay)

# Tests
dotnet test

# Fullstack Docker (Api + Web + SQLite volume; add --profile prod for SQL Server)
docker compose up --build
# → api: http://localhost:8080/swagger , web: http://localhost:5000 , sqlserver: 1433 (prod profile)
```

Default seed (on first run): 5 hostels, ~60 rooms, 20 students, 15 apps, 8 allocations — see `src/HostelSystem.Infrastructure/Persistence/DataSeeder.cs:1`. Reset: `dotnet run --project src/HostelSystem.Api -- --seed-reset`.

## Architecture

```
src/HostelSystem.Domain       — Entities (Hostel, Room, Student, RoomApplication, RoomAllocation, Payment), Enums, DomainEvents
src/HostelSystem.Application  — CQRS Commands/Queries (MediatR), DTOs, Interfaces (IRepositories, IPaymentGateway, IQrCodeService, IEmailService), Behaviors (Validation/Logging), EventHandlers (Email)
src/HostelSystem.Infrastructure — EF Core (AppDbContext, AppIdentityDbContext, Migrations, EntityConfigurations), Repositories, Services (PaystackGateway+QrCodeService, EmailService console/SMTP, MemoryCache), DataSeeder, DependencyInjection (DatabaseProvider switch)
src/HostelSystem.Identity     — ASP.NET Identity (ApplicationUser, AppIdentityDbContext, TokenService, IdentitySeeder)
src/HostelSystem.Api          — Controllers (Auth, Hostels, Rooms, Applications, Allocations, Students, Payments), Program.cs (JWT, RateLimiting auth, Health, Swagger), Middlewares
src/HostelSystem.Web          — Blazor Server (AuthService+JwtAuthenticationStateProvider+AuthHeaderHandler 401→refresh, Login/Register/Logout, NavMenu role-based, Hostels/HostelDetail, Applications, Allocations, Payments list + QR, Admin Dashboard/Pending/Allocations/Payments/Hostels)

docs/ — database-provider, backup-restore, retention-policy, schema (Mermaid ERD), query-review, email-setup
postman/ — Hestia_Postman.json
```

## Configuration — Secrets (never commit)

User Secrets (dev):

```bash
dotnet user-secrets set "Jwt:Secret" "DEV-SECRET-32+CHARS..." --project src/HostelSystem.Api/HostelSystem.Api.csproj
dotnet user-secrets set "Paystack:SecretKey" "sk_test_..." --project src/HostelSystem.Api/HostelSystem.Api.csproj
dotnet user-secrets set "Paystack:PublicKey" "pk_test_..." --project src/HostelSystem.Api/HostelSystem.Api.csproj
# optional SMTP Mailtrap
dotnet user-secrets set "Smtp:Host" "smtp.mailtrap.io" --project src/HostelSystem.Api/HostelSystem.Api.csproj
```

Env / Docker: `Jwt__Secret`, `Paystack__SecretKey`, `Smtp__Host`, `ConnectionStrings__DefaultConnection`, `DatabaseProvider` (Sqlite|SqlServer). See `.env.example`.

## API Reference (v1.0)

Base: `/api/v1.0` — Swagger enumerates all.

- `POST /Auth/register` {email,password,firstName,lastName,studentNumber,gender} → {accessToken,refreshToken} + persists Student. `POST /Auth/login`, `POST /Auth/refresh`, `POST /Auth/logout`
- `GET /Hostels?onlyActive&page&pageSize` (anon, cached), `GET /Hostels/{id}`, `POST /Hostels` (Admin), `PUT /Hostels/{id}`, `POST /Hostels/{id}/deactivate|activate`
- `GET /Rooms/available/{hostelId}` (anon), `GET /Rooms?hostelId&isAvailable&search&page&pageSize`, `GET /Rooms/{id}`, `POST /Rooms` (Admin, 409 dup), `PUT /Rooms/{id}` (Admin, Version 409), `DELETE /Rooms/{id}` (Admin, occupied 400)
- `POST /Applications` {studentId,roomId}, `GET /Applications/me`, `GET /Applications/pending?page&pageSize` (Admin), `GET /Applications?page&status&search` (Admin), `POST /Applications/{id}/approve` (Admin), `POST /Applications/{id}/reject` {reason} (Admin), `POST /Applications/{id}/cancel` {reason} (Student owner or Admin)
- `GET /Allocations/me`, `GET /Allocations/{id}`, `GET /Allocations?page&isActive` (Admin), `POST /Allocations/{id}/checkout` (owner/Admin, releases room)
- `GET /Students/me`, `GET /Students/{id}` (Admin)
- `POST /Payments/initiate` {paymentId,email} → {transactionReference,amount,authorizationUrl}, `GET /Payments/me`, `GET /Payments?page&status&search` (Admin), `GET /Payments/{reference}/verify` (polling), `GET /Payments/{reference}/qr?format=png|svg` (auth), `POST /Payments/webhook` (Paystack signature), `POST /Payments/refund` (Admin)

All paged lists return `X-Total-Count` / `X-Total-Pages`. Room `Update` uses `Version` concurrency → 409 Conflict on clash. Application/Payment status transitions follow `docs/schema.md`.

## Postman

Import `postman/Hestia_Postman.json` — collections for Auth, Hostels, Rooms, Applications, Allocations, Payments with example payloads and expected responses. Environment vars: `baseUrl`, `accessToken`.

## Tests & CI

- Unit: `tests/HostelSystem.UnitTests` (Domain + Application handlers, Result tests) — `global using Xunit` via `GlobalUsings.cs`, Moq+FluentAssertions.
- Integration: `tests/HostelSystem.IntegrationTests` (`CustomWebApplicationFactory` with `AppDbContext`+`AppIdentityDbContext` on temp SQLite, JWT test secret).

CI: `.github/workflows/ci.yml` — restore → build Release → test (trx+coverage) → docker build Api/Web → compose config check (runs on push/PR to `develop`/`main`).

## Paystack + QR

`Paystack:SecretKey` via user-secrets (never committed). `GET /Payments/{ref}/qr` encodes `{reference,amount,currency}` as JSON via QRCoder (ECC Q) — scannable. Web `Payments.razor` shows QR after initiate; `PaymentQr.razor` (`/payments/{ref}`) renders `<img src=/api/v1.0/Payments/{ref}/qr>`. Webhook verifies `x-paystack-signature` HMAC-SHA512. Verify endpoint is polling fallback.

## Docs

- `docs/database-provider.md` — provider switch (env `DatabaseProvider`)
- `docs/backup-restore.md` — SQL Server RTO/RPO, PITR
- `docs/retention-policy.md` — archival jobs
- `docs/schema.md` — Mermaid ERD + indexes
- `docs/query-review.md` — Include→Select audit
- `docs/email-setup.md` — SMTP/Mailtrap + console fallback

## License

MIT — team project.
