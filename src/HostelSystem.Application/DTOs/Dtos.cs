namespace HostelSystem.Application.DTOs;

public record HostelDto(
    int Id,
    string Name,
    string? Description,
    string Address,
    bool IsActive,
    int TotalCapacity,
    int TotalOccupancy,
    int AvailableSpots);

public record RoomDto(
    int Id,
    string RoomNumber,
    int Capacity,
    int CurrentOccupancy,
    decimal PricePerSemester,
    bool IsAvailable,
    int HostelId,
    string HostelName);

public record StudentDto(
    int Id,
    string StudentNumber,
    string FullName,
    string? PhoneNumber,
    bool IsCurrentlyAllocated);

public record ApplicationDto(
    int Id,
    int StudentId,
    int RoomId,
    string Status,
    DateTime ApplicationDate,
    DateTime? ReviewedOn,
    string? ReviewedBy,
    string? RejectionReason);

public record AllocationDto(
    int Id,
    int StudentId,
    string StudentName,
    int RoomId,
    string RoomNumber,
    string HostelName,
    DateTime AllocationDate,
    bool IsActive);

public record PaymentDto(
    int Id,
    decimal Amount,
    string Status,
    string? TransactionReference,
    DateTime? PaidOn,
    DateTime DueDate,
    bool IsOverdue);
