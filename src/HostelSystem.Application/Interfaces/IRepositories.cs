using HostelSystem.Domain.Entities;

namespace HostelSystem.Application.Interfaces;

public interface IHostelRepository
{
    Task<Hostel?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<List<Hostel>> GetAllAsync(CancellationToken ct = default);
    Task<List<Hostel>> GetActiveAsync(CancellationToken ct = default);
    Task<(List<Hostel> Items, int TotalCount)> GetPagedAsync(bool onlyActive, int page, int pageSize, CancellationToken ct = default);
    Task AddAsync(Hostel hostel, CancellationToken ct = default);
    void Update(Hostel hostel);
    void Delete(Hostel hostel);
}

public interface IRoomRepository
{
    Task<Room?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<List<Room>> GetByHostelIdAsync(int hostelId, CancellationToken ct = default);
    Task<List<Room>> GetAvailableByHostelIdAsync(int hostelId, CancellationToken ct = default);
    Task<(List<Room> Items, int TotalCount)> GetPagedAsync(int? hostelId, bool? isAvailable, string? search, int page, int pageSize, CancellationToken ct = default);
    Task<bool> ExistsByRoomNumberAsync(int hostelId, string roomNumber, int? excludeId = null, CancellationToken ct = default);
    Task AddAsync(Room room, CancellationToken ct = default);
    void Update(Room room);
    void Delete(Room room);
}

public interface IStudentRepository
{
    Task<Student?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Student?> GetByUserIdAsync(string userId, CancellationToken ct = default);
    Task<Student?> GetByStudentNumberAsync(string studentNumber, CancellationToken ct = default);
    Task AddAsync(Student student, CancellationToken ct = default);
    void Update(Student student);
}

public interface IApplicationRepository
{
    Task<RoomApplication?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<List<RoomApplication>> GetByStudentIdAsync(int studentId, CancellationToken ct = default);
    Task<List<RoomApplication>> GetPendingAsync(CancellationToken ct = default);
    Task AddAsync(RoomApplication application, CancellationToken ct = default);
    void Update(RoomApplication application);
}

public interface IAllocationRepository
{
    Task<RoomAllocation?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<RoomAllocation?> GetActiveByStudentIdAsync(int studentId, CancellationToken ct = default);
    Task<RoomAllocation?> GetByApplicationIdAsync(int applicationId, CancellationToken ct = default);
    Task AddAsync(RoomAllocation allocation, CancellationToken ct = default);
    void Update(RoomAllocation allocation);
}

public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Payment?> GetByTransactionReferenceAsync(string reference, CancellationToken ct = default);
    Task<Payment?> GetByAllocationIdAsync(int allocationId, CancellationToken ct = default);
    Task<List<Payment>> GetByStudentIdAsync(int studentId, CancellationToken ct = default);
    Task AddAsync(Payment payment, CancellationToken ct = default);
    void Update(Payment payment);
}
