# Data Retention & Archival Policy

Applies to `HostelSystem` (domain DB) and `Identity` (RefreshTokens).

## Principles

- **Privacy by necessity** — keep PII only as long as required for operations, audit, or legal hold.
- **Performance** — archive cold rows to keep hot tables small and indexes selective.
- **Reversibility** — archival is soft-delete / move to archive tables, not destructive hard delete for 1 year.

## Classification

| Data | Classification | Notes |
|------|---------------|-------|
| `Students` + `ApplicationUser` | PII — retain | Delete after graduation + 2 years unless legal hold |
| `RoomApplications` (Pending/Approved/Rejected/Cancelled) | Operational | Hot for 1 academic year, then archive |
| `RoomAllocations` (active / checked-out) | Operational | Active rows always hot; checked-out archived after 1 year |
| `Payments` | Financial | 7 years (audit), then anonymize |
| `RefreshTokens` | Ephemeral | Short-lived; purge aggressively |
| `AspNetUsers` / logs | Audit | Per identity policy |

## Policy by Table

### `RoomApplications`

- **Hot window:** Current academic year + 1 previous year. Queried by `IX_RoomApplications_Status` and `IX_RoomApplications_StudentId_Status`.
- **Archive after:** 12 months after `ReviewedOn` (or `ApplicationDate` if never reviewed and `Cancelled`).
- **Archive destination:** `RoomApplications_Archive` (same schema, partitioned by `ApplicationDate` year or separate DB).
- **Anonymize after 3 years:** Replace `AdditionalNotes` / `RejectionReason` PII with hash; keep statistics.
- **Implementation:**
  ```sql
  -- Monthly job (SQL Server Agent)
  INSERT INTO RoomApplications_Archive
  SELECT * FROM RoomApplications
  WHERE Status IN ('Approved','Rejected','Cancelled')
    AND COALESCE(ReviewedOn, ApplicationDate) < DATEADD(month, -12, GETUTCDATE());

  DELETE FROM RoomApplications
  WHERE Id IN (SELECT Id FROM RoomApplications_Archive);
  ```
  Run after backup, inside a transaction, log row counts.

### `RoomAllocations`

- **Active** (`IsActive=1`): never archived.
- **Checked-out** (`IsActive=0` and `CheckOutDate` not null): archive after 12 months, keep FK to `RoomApplications_Archive`.
- **Index `IX_RoomAllocations_StudentId_IsActive` ensures active lookup stays fast even with archived rows removed from hot table.**

### `Payments`

- **Retention:** 7 years (financial compliance). Do not delete within 7 years.
- **After 7 years:** Anonymize `StudentId` → `NULL` or pseudonymized ID, keep amount/status for reporting; or move to `Payments_Archive`.
- **Partition by `DueDate` year** for efficient purge.
- **Refresh from domain events:** `PaymentCompleted` events are also in outbox/audit log.

### `RefreshTokens`

- **TTL:** `ExpiresAt` is the max lifetime (e.g., 7 days). Tokens are revoked on logout.
- **Purge job:** Daily, delete `RevokedAt IS NOT NULL` OR `ExpiresAt < DATEADD(day, -7, GETUTCDATE())`.
  ```sql
  DELETE FROM RefreshTokens
  WHERE RevokedAt IS NOT NULL
     OR ExpiresAt < DATEADD(day, -7, GETUTCDATE());
  ```
  Covered by `IX_RefreshTokens_ExpiresAt` and `IX_RefreshTokens_UserId`.

### `Students` / `AspNetUsers`

- **Keep** while student has active allocation or pending application.
- **After graduation:** soft-deactivate; after 2 years hard-delete or anonymize per university policy and GDPR-like request. Cascade carefully due to `Restrict` FK — archive child rows first.

## Implementation Notes

- **Do not cascade FK deletes** (all relationships are `Restrict`). Archival must order: `Payments` → `RoomAllocations` → `RoomApplications` → `Students`.
- **Use batch deletes** (`TOP 1000` loop) to avoid long locks:
  ```sql
  WHILE EXISTS (SELECT 1 FROM RoomApplications WHERE ...)
  BEGIN
    DELETE TOP (1000) FROM RoomApplications WHERE ...;
    WAITFOR DELAY '00:00:01';
  END
  ```
- **Audit:** Log archival job output (`Archived N rows from X to X_Archive on <date> by <job>`).
- **SQLite dev:** Retention jobs are not scheduled; use `DataSeeder.ResetAsync()` or `dotnet run -- --seed-reset` to simulate clean state. Retention logic is SQL Server–oriented.

## Monitoring

- Alert if `RoomApplications` count > 50k hot rows (indicates archival lag).
- Alert if `RefreshTokens` count > 100k (purge failure).
- Dashboard: rows by status / age bucket.

## Approval & Review

- Policy owner: Team Lead + Data Protection Officer.
- Review: Annually or after schema changes.
- Any destructive purge beyond this policy requires change request + backup verification.

---
Version: 1.0 — 2026-08-25
