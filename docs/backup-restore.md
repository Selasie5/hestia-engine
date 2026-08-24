# Backup & Restore Runbook

Scope: **Production SQL Server**. SQLite dev backups are covered briefly at the end.

## Summary

- **RTO:** ≤ 1 hour (restore + verification + DNS cutover)
- **RPO:** ≤ 15 minutes (transaction log backups)
- **Retention:** 30 days full + log chain (see `docs/retention-policy.md`)
- **Encryption:** TDE or encrypted backups (`WITH ENCRYPTION`) + key backup offsite
- **Test:** Restore drill monthly — last successful restore must be < 30 days old

## Architecture

```
AppDbContext (HostelSystem)  ──┐
                                ├─→ SQL Server (Host: sqlserver / Azure SQL)
AppIdentityDbContext (Identity) ─┘       ├── HostelSystem (domain db)
                                         └── HostelSystem_Identity (or shared DB)
```

Both contexts may share one database or use two. Runbook covers both.

## Backup Strategy (SQL Server)

### 1. Full Backup — Nightly (02:00 UTC)

```sql
BACKUP DATABASE [HostelSystem]
TO DISK = N'/var/opt/mssql/backups/HostelSystem_Full_$(date +%Y%m%d_%H%M).bak'
WITH COMPRESSION, CHECKSUM, INIT, STATS = 10;

BACKUP DATABASE [HostelSystem_Identity]
TO DISK = N'/var/opt/mssql/backups/HostelSystem_Identity_Full_$(date +%Y%m%d_%H%M).bak'
WITH COMPRESSION, CHECKSUM, INIT;
```

- Agent job or cron; verify with `RESTORE VERIFYONLY`.
- Offsite copy: `azcopy` / `aws s3 cp` to geo-redundant storage (`hostelsystem-backups`).

### 2. Differential — Every 6 hours (optional if DB < 20 GB)

```sql
BACKUP DATABASE [HostelSystem]
TO DISK = N'/var/opt/mssql/backups/HostelSystem_Diff_$(date +%Y%m%d_%H%M).bak'
WITH DIFFERENTIAL, COMPRESSION, CHECKSUM;
```

### 3. Transaction Log — Every 15 minutes

```sql
BACKUP LOG [HostelSystem]
TO DISK = N'/var/opt/mssql/backups/HostelSystem_Log_$(date +%Y%m%d_%H%M).trn'
WITH COMPRESSION, CHECKSUM;
```

Requires `FULL` recovery model:

```sql
ALTER DATABASE [HostelSystem] SET RECOVERY FULL;
ALTER DATABASE [HostelSystem_Identity] SET RECOVERY FULL;
```

### 4. Verification

After each backup:

```sql
RESTORE VERIFYONLY FROM DISK = N'/var/opt/mssql/backups/HostelSystem_Full_....bak' WITH CHECKSUM;
```

Alert on failure (Ops channel).

## Restore Procedures

### A. Point-in-Time Restore (PITR) — Recommended

```sql
-- Tail-log backup first (if server still online)
BACKUP LOG [HostelSystem] TO DISK = N'/var/opt/mssql/backups/HostelSystem_Tail.trn' WITH NORECOVERY;

-- Restore sequence
RESTORE DATABASE [HostelSystem] FROM DISK = N'/var/opt/mssql/backups/HostelSystem_Full_20260824_0200.bak' WITH NORECOVERY, REPLACE;
RESTORE DATABASE [HostelSystem] FROM DISK = N'/var/opt/mssql/backups/HostelSystem_Diff_20260825_0200.bak' WITH NORECOVERY; -- if exists
RESTORE LOG [HostelSystem] FROM DISK = N'/var/opt/mssql/backups/HostelSystem_Log_20260825_0800.trn' WITH NORECOVERY;
RESTORE LOG [HostelSystem] FROM DISK = N'/var/opt/mssql/backups/HostelSystem_Tail.trn'
WITH RECOVERY, STOPAT = '2026-08-25T07:45:00';

-- Repeat for Identity DB
```

### B. Full Restore (latest good backup, data loss = since last backup)

```sql
RESTORE DATABASE [HostelSystem] FROM DISK = N'/var/opt/mssql/backups/HostelSystem_Full_20260825_0200.bak'
WITH RECOVERY, REPLACE, CHECKSUM;
```

### C. Docker / Local SQL Server (dev prod-like)

```bash
# Inside sqlserver container
/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'YourStrong!Passw0rd' -C -Q "RESTORE DATABASE [HostelSystem] FROM DISK = '/var/opt/mssql/backups/HostelSystem_Full_...bak' WITH REPLACE, RECOVERY"
```

## Application Steps During Restore

1. **Stop writes:** Scale API to 0 or enable maintenance mode (`ASPNETCORE_ENVIRONMENT=Maintenance` → 503).
2. **Restore** per section above.
3. **Apply pending migrations** if backup was older than latest migration:
   ```bash
   dotnet ef database update --connection "Server=...;Database=HostelSystem;..."
   # OR app startup does MigrateAsync automatically
   ```
4. **Verify:** `SELECT COUNT(*) FROM Rooms`, `SELECT COUNT(*) FROM RoomApplications WHERE Status='Pending'`, check `__EFMigrationsHistory`.
5. **Smoke test:** `GET /health`, `POST /api/auth/login`, `GET /api/rooms/available?hostelId=1`.
6. **Bring API back** and monitor logs.

## Azure SQL / Managed Variant

- Enable **PITR** (7–35 days), **LTR** (weekly/monthly/yearly).
- Geo-restore and auto-failover group for DR.
- Same verification steps; use `az sql db restore` / portal.

## SQLite (Development)

Dev DB is `hostelsystem.db` / `/data/hostelsystem.db` (volume `sqlite-data`).

- **Backup:** File copy + `sqlite3` dump:
  ```bash
  sqlite3 /data/hostelsystem.db ".backup '/backups/hostelsystem_$(date +%Y%m%d).db'"
  sqlite3 /data/hostelsystem.db ".dump" | gzip > /backups/hostelsystem_$(date +%Y%m%d).sql.gz
  # Or Docker volume backup
  docker run --rm -v hostelsystem_sqlite-data:/data -v $(pwd)/backups:/backup alpine cp /data/hostelsystem.db /backup/
  ```
- **Restore:**
  ```bash
  sqlite3 /data/hostelsystem.db < /backups/hostelsystem_YYYYMMDD.sql
  # Or volume restore + dotnet ef database update
  dotnet run -- --seed-reset-only  # if you want a clean realistic dataset
  ```
- **Reset mechanism (code):** See `src/HostelSystem.Infrastructure/Persistence/DataSeeder.cs:ResetAsync` and CLI `dotnet run -- --seed-reset`

## Runbook Checklist (Print)

- [ ] Identify failure point / STOPAT time
- [ ] Tail-log backup if possible
- [ ] Choose backup set (full + diff + logs)
- [ ] Maintenance mode on
- [ ] Restore sequence with `NORECOVERY` → `RECOVERY`
- [ ] `RESTORE VERIFYONLY` + `DBCC CHECKDB`
- [ ] Migrations updated
- [ ] Smoke tests pass
- [ ] Maintenance mode off
- [ ] Post-incident: file incident report + adjust RPO/RTO if needed

## Monitoring & Alerts

- Job failures → PagerDuty / email.
- Backup age > 26h → alert.
- Storage free < 20% → alert.
- Monthly restore drill ticket auto-created.

## Secrets

- Never commit `SA_PASSWORD`; use Key Vault / Docker secrets / `User Secrets`.
- Encrypt backup files at rest (`gpg` or storage encryption).

---
Last verified: 2026-08-25. Owner: Hestia Engine team.
