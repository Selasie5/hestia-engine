# Hestia Engine — Hostel Room Allocation System

Clean Architecture + CQRS/MediatR + Blazor Server — hostel browsing, application workflow (submit → approve/reject/cancel), allocation & checkout, Paystack payments + QR, role-based portals (Student/Admin).

**Stack:** .NET 10, ASP.NET Core API (JWT + Identity), EF Core (SQLite dev / SQL Server prod pluggable via `DatabaseProvider`), Blazor Server (`HostelSystem.Web`), MediatR, FluentValidation, QRCoder, Serilog, Docker Compose, GitHub Actions CI.

## Quick Start ( < 10 min )

```bash
git clone https://github.com/Selasie5/hestia-engine.git
cd hestia-engine

# API — SQLite auto-migrates + seeds on first run
dotnet run --project src/HostelSystem.Api/HostelSystem.Api.csproj --urls http://localhost:8080
# → http://localhost:8080/health
# → http://localhost:8080/swagger

# Web — Blazor Server
dotnet run --project src/HostelSystem.Web/HostelSystem.Web.csproj --urls http://localhost:5000
# → http://localhost:5000  (login → hostels → apply → allocation → Paystack)

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
src/HostelSystem.Web          — Interactive Blazor Server portal with protected-browser-storage JWT auth, role-aware navigation, hostel/room discovery, application tracking, allocation-led Paystack checkout, payment history/QR, and Admin Dashboard/Applications/Allocations/Payments/Hostels

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

The web project resolves its API base address in this order: `ApiHostPort`, `ApiBaseUrl`, then `http://localhost:8080`. The Quick Start commands match the checked-in `ApiBaseUrl`. If you use the API launch profile on port `5156`, override the web setting with `ApiBaseUrl=http://localhost:5156`.

## Portal Workflows

### Student

1. Sign in and browse active hostels at `/hostels`.
2. Open a hostel, choose an available room, and submit an application.
3. Track or cancel the request at `/applications`.
4. When an administrator approves the request, the backend atomically marks the application approved, updates room occupancy, creates an active allocation, and creates a pending payment due in 14 days.
5. Open `/allocations` and select **Make payment**. Paystack checkout starts from the payment attached to that allocation.
6. Paystack returns to `/payments?reference=...` (or `trxref=...`); the payment page verifies the reference and refreshes payment history. `/payments/{reference}` provides authenticated payment details and downloadable PNG/SVG QR codes.

### Administrator

- `/admin/applications` reviews pending requests. Approving a request creates the allocation and pending payment automatically; no separate allocation action is required.
- `/admin/hostels` uses a guided modal to create a hostel and its initial rooms. Adding rooms to an existing hostel uses a separate room/review step flow.
- `/admin/allocations` filters active and checked-out allocations and supports checkout.
- `/admin/payments` filters, verifies, and refunds payments.

Authenticated web requests read the JWT from the active Blazor circuit and attach it directly to protected API calls. Initial protected data loading is deferred until the interactive circuit is available, avoiding browser-storage JavaScript interop during static rendering.

Meaningful list state is retained in URLs so refreshes and shared links preserve context:

- `/hostels?page=2`
- `/admin/applications?page=2`
- `/admin/allocations?page=2&status=active`
- `/admin/payments?page=2&status=Pending`

Transient state such as loading indicators, confirmation messages, and unsaved modal fields remains local to the component.

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

`Paystack:SecretKey` is supplied through user-secrets or environment variables and must never be committed. Payment initiation accepts an optional `callbackUrl`; the allocation page sends the web payment-history URL. Paystack may append either `reference` or `trxref`, both of which the web page accepts and verifies.

Configure these Paystack URLs for the deployed services:

```text
Callback URL: https://hestia-web-kna0.onrender.com/payments
Webhook URL:  https://hestia-api-tqud.onrender.com/api/v1.0/Payments/webhook
```

For the Quick Start configuration, the local browser callback is `http://localhost:5000/payments` (`http://localhost:5182/payments` when using the web HTTP launch profile). Paystack cannot deliver webhooks to localhost; use a public tunnel when testing webhook delivery locally.

`GET /Payments/{reference}/qr?format=png|svg` is authenticated and encodes `{reference,amount,currency}` via QRCoder (ECC Q). The `/payments/{reference}` page retrieves the QR with the signed-in student's JWT. The webhook validates `x-paystack-signature` using HMAC-SHA512 and is the primary completion path; `GET /Payments/{reference}/verify` is the authenticated polling fallback.

## Web UI Conventions

- Authenticated navigation uses an inset sidebar and a user profile menu with sign-out.
- Main content uses responsive page padding and horizontally scrollable data tables on narrow screens.
- The interface uses a square-cornered component language (`border-radius: 0`) across buttons, cards, inputs, alerts, tables, and overlays. Loading spinners remain circular so their motion remains recognizable.
- Hostel room cards use a representative room image, availability/occupancy information, and a full-width application action with progress and feedback states.
- Pages provide distinct loading, empty, error, success, and disabled states rather than displaying raw API or HTML responses.

## Docs

- `docs/database-provider.md` — provider switch (env `DatabaseProvider`)
- `docs/backup-restore.md` — SQL Server RTO/RPO, PITR
- `docs/retention-policy.md` — archival jobs
- `docs/schema.md` — Mermaid ERD + indexes
- `docs/query-review.md` — Include→Select audit
- `docs/email-setup.md` — SMTP/Mailtrap + console fallback
- `docs/render-deployment.md` — Render Blueprint deployment, persistence, Paystack, and verification

## Deployed Services

- API: https://hestia-api-tqud.onrender.com
- Swagger: https://hestia-api-tqud.onrender.com/swagger/index.html
- Web: https://hestia-web-kna0.onrender.com

## License

MIT — team project.
