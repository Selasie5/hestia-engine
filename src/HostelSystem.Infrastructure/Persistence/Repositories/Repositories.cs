using HostelSystem.Application.Interfaces;
using HostelSystem.Domain.Entities;
using HostelSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HostelSystem.Infrastructure.Persistence.Repositories;

// ============================================================
// TEMPLATE: Repository pattern implementation.
// For your remaining entities, copy this pattern:
//   1. Implement the interface from Application.Interfaces
//   2. Inject AppDbContext (via constructor)
//   3. Use FindAsync for single, LINQ for queries, Add/Update methods
// ============================================================

public class HostelRepository : IHostelRepository
{
    private readonly AppDbContext _context;

    public HostelRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Hostel?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await _context.Hostels
            .Include(h => h.Rooms)
            .FirstOrDefaultAsync(h => h.Id == id, ct);
    }

    public async Task<List<Hostel>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.Hostels
            .AsNoTracking()
            .Include(h => h.Rooms)
            .ToListAsync(ct);
    }

    public async Task<List<Hostel>> GetActiveAsync(CancellationToken ct = default)
    {
        return await _context.Hostels
            .AsNoTracking()
            .Where(h => h.IsActive)
            .Include(h => h.Rooms)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Hostel hostel, CancellationToken ct = default)
    {
        await _context.Hostels.AddAsync(hostel, ct);
    }

    public void Update(Hostel hostel)
    {
        _context.Hostels.Update(hostel);
    }

    public void Delete(Hostel hostel)
    {
        _context.Hostels.Remove(hostel);
    }
}

public class RoomRepository : IRoomRepository
{
    private readonly AppDbContext _context;

    public RoomRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Room?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await _context.Rooms
            .Include(r => r.Hostel)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task<List<Room>> GetByHostelIdAsync(int hostelId, CancellationToken ct = default)
    {
        return await _context.Rooms
            .AsNoTracking()
            .Where(r => r.HostelId == hostelId)
            .Include(r => r.Hostel)
            .ToListAsync(ct);
    }

    public async Task<List<Room>> GetAvailableByHostelIdAsync(int hostelId, CancellationToken ct = default)
    {
        return await _context.Rooms
            .AsNoTracking()
            .Where(r => r.HostelId == hostelId && r.IsAvailable && r.CurrentOccupancy < r.Capacity)
            .Include(r => r.Hostel)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Room room, CancellationToken ct = default)
    {
        await _context.Rooms.AddAsync(room, ct);
    }

    public void Update(Room room)
    {
        _context.Rooms.Update(room);
    }

    public void Delete(Room room)
    {
        _context.Rooms.Remove(room);
    }
}

public class StudentRepository : IStudentRepository
{
    private readonly AppDbContext _context;

    public StudentRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Student?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await _context.Students.FindAsync([id], ct);
    }

    public async Task<Student?> GetByUserIdAsync(string userId, CancellationToken ct = default)
    {
        return await _context.Students
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);
    }

    public async Task<Student?> GetByStudentNumberAsync(string studentNumber, CancellationToken ct = default)
    {
        return await _context.Students
            .FirstOrDefaultAsync(s => s.StudentNumber == studentNumber, ct);
    }

    public async Task AddAsync(Student student, CancellationToken ct = default)
    {
        await _context.Students.AddAsync(student, ct);
    }

    public void Update(Student student)
    {
        _context.Students.Update(student);
    }
}

public class ApplicationRepository : IApplicationRepository
{
    private readonly AppDbContext _context;

    public ApplicationRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Domain.Entities.RoomApplication?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await _context.RoomApplications
            .Include(a => a.Student)
            .Include(a => a.Room)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    public async Task<List<Domain.Entities.RoomApplication>> GetByStudentIdAsync(int studentId, CancellationToken ct = default)
    {
        return await _context.RoomApplications
            .AsNoTracking()
            .Where(a => a.StudentId == studentId)
            .OrderByDescending(a => a.ApplicationDate)
            .ToListAsync(ct);
    }

    public async Task<List<Domain.Entities.RoomApplication>> GetPendingAsync(CancellationToken ct = default)
    {
        return await _context.RoomApplications
            .AsNoTracking()
            .Where(a => a.Status == Domain.Enums.ApplicationStatus.Pending)
            .Include(a => a.Student)
            .Include(a => a.Room)
            .OrderBy(a => a.ApplicationDate)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Domain.Entities.RoomApplication application, CancellationToken ct = default)
    {
        await _context.RoomApplications.AddAsync(application, ct);
    }

    public void Update(Domain.Entities.RoomApplication application)
    {
        _context.RoomApplications.Update(application);
    }
}

public class AllocationRepository : IAllocationRepository
{
    private readonly AppDbContext _context;

    public AllocationRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<RoomAllocation?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await _context.RoomAllocations
            .Include(a => a.Student)
            .Include(a => a.Room)
            .ThenInclude(r => r.Hostel)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    public async Task<RoomAllocation?> GetActiveByStudentIdAsync(int studentId, CancellationToken ct = default)
    {
        return await _context.RoomAllocations
            .FirstOrDefaultAsync(a => a.StudentId == studentId && a.IsActive, ct);
    }

    public async Task<RoomAllocation?> GetByApplicationIdAsync(int applicationId, CancellationToken ct = default)
    {
        return await _context.RoomAllocations
            .FirstOrDefaultAsync(a => a.ApplicationId == applicationId, ct);
    }

    public async Task AddAsync(RoomAllocation allocation, CancellationToken ct = default)
    {
        await _context.RoomAllocations.AddAsync(allocation, ct);
    }

    public void Update(RoomAllocation allocation)
    {
        _context.RoomAllocations.Update(allocation);
    }
}

public class PaymentRepository : IPaymentRepository
{
    private readonly AppDbContext _context;

    public PaymentRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Payment?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await _context.Payments.FindAsync([id], ct);
    }

    public async Task<Payment?> GetByTransactionReferenceAsync(string reference, CancellationToken ct = default)
    {
        return await _context.Payments
            .FirstOrDefaultAsync(p => p.TransactionReference == reference, ct);
    }

    public async Task<Payment?> GetByAllocationIdAsync(int allocationId, CancellationToken ct = default)
    {
        return await _context.Payments
            .FirstOrDefaultAsync(p => p.AllocationId == allocationId, ct);
    }

    public async Task<List<Payment>> GetByStudentIdAsync(int studentId, CancellationToken ct = default)
    {
        return await _context.Payments
            .AsNoTracking()
            .Where(p => p.StudentId == studentId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Payment payment, CancellationToken ct = default)
    {
        await _context.Payments.AddAsync(payment, ct);
    }

    public void Update(Payment payment)
    {
        _context.Payments.Update(payment);
    }
}
