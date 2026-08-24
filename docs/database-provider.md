# Database Provider Strategy

## Decision

| Environment | Provider | Connection | Rationale |
|-------------|----------|------------|-----------|
| **Development** | SQLite (`Microsoft.EntityFrameworkCore.Sqlite`) | `Data Source=hostelsystem.db` or `Data Source=/data/hostelsystem.db` (Docker) | Zero-setup, file-based, fast integration tests, matches current `InitialCreate` / `InitialIdentity` migrations |
| **Production** | SQL Server (`Microsoft.EntityFrameworkCore.SqlServer`) | `Server=sqlserver,1433;Database=HostelSystem;...` | Durability, concurrent writers, backups, PITR, managed identity |

This is **config-driven** — no code change required to switch.

## How It Works

Both `HostelSystem.Infrastructure` (`AppDbContext`) and `HostelSystem.Identity` (`AppIdentityDbContext`) read the provider at startup:

```csharp
var provider = configuration["DatabaseProvider"]
               ?? configuration["Database:Provider"]
               ?? Environment.GetEnvironmentVariable("DATABASE_PROVIDER")
               ?? "Sqlite";

if (provider.Equals("SqlServer", ...))
    options.UseSqlServer(connectionString, o => o.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null));
else
    options.UseSqlite(connectionString);
```

Sources checked in order: `appsettings.json` → `appsettings.{Environment}.json` → environment variable / Docker secret.

- `DatabaseProvider=Sqlite` (default) → SQLite
- `DatabaseProvider=SqlServer` / `Sql_Server` / `mssql` → SQL Server

## Configuration

### `appsettings.json`

```json
{
  "DatabaseProvider": "Sqlite",
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=hostelsystem.db"
  }
}
```

### `appsettings.Production.json` (example — not committed with secrets)

```json
{
  "DatabaseProvider": "SqlServer",
  "ConnectionStrings": {
    "DefaultConnection": "Server=sqlserver,1433;Database=HostelSystem;User Id=sa;Password=${SQL_PASSWORD};TrustServerCertificate=True;Encrypt=True"
  }
}
```

### Environment variables

| Variable | Example |
|----------|---------|
| `DatabaseProvider` | `SqlServer` |
| `DATABASE_PROVIDER` | `SqlServer` (fallback) |
| `ConnectionStrings__DefaultConnection` | `Server=...` or `Data Source=/data/hostelsystem.db` |

### Docker Compose

```yaml
services:
  api:
    environment:
      - DatabaseProvider=Sqlite          # dev
      - ConnectionStrings__DefaultConnection=Data Source=/data/hostelsystem.db

  # Production profile (docker compose --profile prod up)
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    profiles: ["prod"]
```

See `docker-compose.yml:1` for the full `sqlserver` service.

### Local `.env`

Copy `.env.example` → `.env`:

```ini
DatabaseProvider=Sqlite
ConnectionStrings__DefaultConnection=Data Source=hostelsystem.db
# DatabaseProvider=SqlServer
# ConnectionStrings__DefaultConnection=Server=localhost,1433;Database=HostelSystem;...
```

## Migrations — Working With Both Providers

### Current State

All migrations were scaffolded against **SQLite** (`InitialCreate`, `InitialIdentity`, `AddRefreshTokens`). Column types are `TEXT`/`INTEGER`. New index migrations (`AddPerformanceIndexes`, `AddRefreshTokenIndexes`) use **provider-agnostic** `CreateIndex` DDL and therefore apply cleanly to both providers.

Existing migrations will run against SQL Server in most cases because SQL Server accepts SQLite types via EF translation, but for production-grade DDL (e.g., `nvarchar`, `datetime2`, `rowversion`) you should regenerate provider-specific migrations.

### Recommended Workflow

1. **Dev loop (SQLite):**
   ```bash
   dotnet ef migrations add MyMigration --project src/HostelSystem.Infrastructure --startup-project src/HostelSystem.Api --context AppDbContext
   dotnet ef database update
   ```

2. **Before production release (SQL Server):**
   ```bash
   # Point to SQL Server temporarily
   $env:DATABASE_PROVIDER="SqlServer"
   $env:ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=HostelSystem_DryRun;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True"

   dotnet ef migrations add MyMigration_SqlServer --context AppDbContext  # review column types
   # OR: keep a single migration set and validate via `dotnet ef migrations script --idempotent` against both providers
   ```

3. **CI validation:**
   Run `dotnet ef migrations script --output migrations.sql --idempotent` and grep for `TEXT` vs `nvarchar(max)` to catch provider drift.

4. **Do not share `__EFMigrationsHistory` between providers** — use separate databases or ensure the script is idempotent.

### Team Lead Coordination

- [ ] Agree to keep dev on SQLite for speed; run nightly CI against SQL Server (Docker `sqlserver` profile).
- [ ] Decide whether to maintain **one migration set** (preferred for number of entities) or **provider-specific sets** under `Migrations/Sqlite` + `Migrations/SqlServer` if DDL diverges (e.g., `rowversion` vs `GUID` concurrency token).
- [ ] Document the outcome in this file’s revision history.

## Concurrency Token

`Room.Version` is currently `GUID` concurrency token (`IsConcurrencyToken()`). Works on both providers. For SQL Server you may migrate to `rowversion` in the future — track as separate migration.

## Verification

```bash
# SQLite (dev)
dotnet run --project src/HostelSystem.Api
# check logs: no UseSqlServer path

# SQL Server (prod dry-run)
DatabaseProvider=SqlServer ConnectionStrings__DefaultConnection="Server=localhost,1433;..." dotnet run --project src/HostelSystem.Api
# verify startup logs and query plan uses IX_Rooms_HostelId_IsAvailable_CurrentOccupancy
```

## Acceptance

- [x] Provider switch documented
- [x] `DatabaseProvider` in `appsettings.json`, `docker-compose.yml`, `.env.example`
- [x] Both DI registrations support `SqlServer` + `Sqlite`
- [x] `Microsoft.EntityFrameworkCore.SqlServer` added to `HostelSystem.Infrastructure` & `HostelSystem.Identity`
