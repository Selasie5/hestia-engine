# Query Review — `Repositories.cs` `Include` Audit

Date: 2026-08-25 • Branch: `feature/perf-indexes-provider-docs` • File: `src/HostelSystem.Infrastructure/Persistence/Repositories/Repositories.cs`

## Summary

The repository layer currently uses eager `Include` / `ThenInclude` in most read paths. This is correct for write-path loads (aggregate hydration) but wasteful for list / query handlers that only need a subset of navigation properties. The availability query `WHERE HostelId = ? AND IsAvailable = 1 AND CurrentOccupancy < Capacity` is now covered by `IX_Rooms_HostelId_IsAvailable_CurrentOccupancy` (see `20260825120000_AddPerformanceIndexes`).

## Findings

| Repository | Method | Current Includes | Issue | Recommendation |
|---|---|---|---|---|
| `HostelRepository` | `GetByIdAsync` | `Include(h => h.Rooms)` | Loads entire room collection for a single hostel lookup. OK if caller needs rooms, wasteful if only hostel header needed. | Add overload `GetByIdHeaderAsync` without `Include`; keep `GetByIdWithRoomsAsync` for detail page. Or project: `Select(h => new HostelDto{..., Rooms = h.Rooms.Select(...)})` and use `AsNoTracking()` + `AsSplitQuery()` for large collections. |
| `HostelRepository` | `GetAllAsync` / `GetActiveAsync` | `Include(h => h.Rooms)` | N hostels × M rooms cartesian product. Serializes full graph. | For list views use projection that selects only `Id, Name, Address, RoomCount` without materializing child entities: `Select(h => new HostelListDto(h.Id, h.Name, h.Rooms.Count))`. If full list is needed, add `.AsSplitQuery()` to avoid cartesian explosion. |
| `RoomRepository` | `GetByIdAsync` | `Include(r => r.Hostel)` | Single aggregate – acceptable. Still prefer `AsNoTracking()` for read if entity isn't tracked for update. | Add `.AsNoTracking()` variant for read-only; keep tracked version for write. Alternatively `Select` to `RoomDto` with `Hostel.Name` only. |
| `RoomRepository` | `GetByHostelIdAsync` | `Include(r => r.Hostel)` | Loads full `Hostel` entity when only `Hostel.Name` is displayed. | Change handler to `Select(r => new RoomDto(r.Id, r.RoomNumber, ..., r.Hostel.Name))` so the join is pushed to SQL. Remove `Include` — `Select` auto-generates the join without materializing the navigation entity. |
| `RoomRepository` | `GetAvailableByHostelIdAsync` | `Include(r => Hostel)` + `Where HostelId && IsAvailable && Occupancy < Capacity` | Same issue + now indexed. Handler `GetAvailableRoomsQueryHandler` does `Select` **after** materialization (in-memory). | **High priority**: push projection to DB: `context.Rooms.Where(...).Select(r => new RoomDto(..., r.Hostel.Name)).ToListAsync()` — removes the `Include` and avoids loading `Room.Allocations`. Keep `AsNoTracking()`. See example below. |
| `ApplicationRepository` | `GetByIdAsync` | `Include(Student)` + `Include(Room)` | Aggregate hydration for approve/reject – correct. | Keep as-is; add `.AsTracking()` explicitly for write path. |
| `ApplicationRepository` | `GetByStudentIdAsync` | *no* `Include` | Minimal – good. Caller gets no student/room details. | If UI needs `RoomNumber` / `HostelName`, add a projected method `GetByStudentIdProjectedAsync` with `Select(a => new ApplicationDto(..., a.Room.RoomNumber, ...))`. Don't add `Include` to the existing method. |
| `ApplicationRepository` | `GetPendingAsync` | `Include(Student)` + `Include(Room)` | Admin review queue – likely needs student name + room number only. Includes load full entities. | Recommend `Select(a => new PendingApplicationDto(...))` with only `Student.FirstName/LastName/StudentNumber` and `Room.RoomNumber/Hostel.Name`. Use `AsNoTracking()`. |
| `AllocationRepository` | `GetByIdAsync` | `Include(Student)` + `Include(Room).ThenInclude(Hostel)` | Deep graph (3 levels). Heavy for simple lookup. | Keep for detail view, but add lightweight `GetActiveAllocationDtoByStudentId` that projects only needed fields. Use `AsNoTracking()`. |
| `AllocationRepository` | `GetActiveByStudentIdAsync` | *no* Include | Good – only PK/FK lookup (`WHERE StudentId && IsActive`). Now covered by `IX_RoomAllocations_StudentId_IsActive`. | No change. Ensure caller doesn't trigger lazy load. |
| `PaymentRepository` | `GetByIdAsync` | `FindAsync` | Ideal – no includes. | No change. |
| `PaymentRepository` | `GetByTransactionReferenceAsync` | `FirstOrDefault` on `TransactionReference` | Unique index exists (`IX_Payments_TransactionReference`) + new `IX_Payments_Status`. | No change; ensure transaction reference lookup is `AsNoTracking()` if read-only. Add method `GetByStudentIdAsync` with `Status` filter using `IX_Payments_StudentId` / `IX_Payments_Status`. |

## Recommended Projection Pattern

```csharp
// Before (inefficient: Include + in-memory Select)
return await _context.Rooms
    .AsNoTracking()
    .Where(r => r.HostelId == hostelId && r.IsAvailable && r.CurrentOccupancy < r.Capacity)
    .Include(r => r.Hostel)
    .ToListAsync(ct);
// then rooms.Select(r => new RoomDto(..., r.Hostel.Name))

// After (efficient: Select pushed to SQL, no Include needed)
return await _context.Rooms
    .AsNoTracking()
    .Where(r => r.HostelId == hostelId && r.IsAvailable && r.CurrentOccupancy < r.Capacity)
    .Select(r => new RoomDto(
        r.Id,
        r.RoomNumber,
        r.Capacity,
        r.CurrentOccupancy,
        r.PricePerSemester,
        r.IsAvailable,
        r.HostelId,
        r.Hostel.Name))
    .ToListAsync(ct);
```

Benefits: single SQL roundtrip, join generated by EF, no materialization of untracked `Hostel` entities, only 8 columns transferred.

## Additional Guidelines

1. **Default to `AsNoTracking()`** for all query handlers / read repositories. Only `GetByIdAsync` for commands should be tracked.
2. **Use `AsSplitQuery()`** when you must `Include` a collection (e.g., `Hostels → Rooms`) and expect > 50 parent rows.
3. **Avoid `Update()` on detached graphs** — use `Attach` + property assignment or fetch-then-mutate.
4. **Paginate list endpoints**: `GetPendingAsync` / `GetByStudentIdAsync` should accept `page, pageSize` and apply `Skip/Take` server-side.
5. **Indexes now support**: verify with `EXPLAIN QUERY PLAN` (SQLite) or `SET STATISTICS IO` (SQL Server) that the new indexes are hit — expected seek on `IX_Rooms_HostelId_IsAvailable_CurrentOccupancy`.

## Action Log

- [x] Created indexes via migration `20260825120000_AddPerformanceIndexes` + `20260825120001_AddRefreshTokenIndexes`.
- [ ] Next PR: add projected `IRoomRepository.GetAvailableProjectedAsync` and update `GetAvailableRoomsQueryHandler` to use it (non-breaking).
- [ ] Next PR: add `HostelRepository.GetSummaryAsync` with `Select` for dashboard lists.
- [ ] Add pagination to `ApplicationRepository` methods.

## References

- `src/HostelSystem.Application/Queries/Rooms/GetAvailableRooms.cs:26-48`
- `src/HostelSystem.Infrastructure/Persistence/Repositories/Repositories.cs:16-264`
- `docs/database-provider.md` — provider switch affects index DDL (same `CreateIndex` syntax works for both SQLite and SQL Server).
