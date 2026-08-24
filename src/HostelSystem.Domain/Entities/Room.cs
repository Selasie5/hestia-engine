using System.ComponentModel.DataAnnotations;
using HostelSystem.Domain.Exceptions;

namespace HostelSystem.Domain.Entities;

public class Room : Entity
{
    public int HostelId { get; private set; }
    public Hostel Hostel { get; private set; } = null!;
    public string RoomNumber { get; private set; }
    public int Capacity { get; private set; }
    public int CurrentOccupancy { get; private set; }
    public decimal PricePerSemester { get; private set; }
    public bool IsAvailable { get; private set; }

    [System.ComponentModel.DataAnnotations.ConcurrencyCheck]
    public Guid Version { get; private set; } = Guid.NewGuid();

    private readonly List<RoomAllocation> _allocations = [];
    public IReadOnlyCollection<RoomAllocation> Allocations => _allocations.AsReadOnly();

    private Room() { } // EF Core

    public Room(string roomNumber, int capacity, decimal pricePerSemester, int hostelId)
    {
        if (string.IsNullOrWhiteSpace(roomNumber))
            throw new BusinessRuleViolationException("Room number cannot be empty.");

        if (capacity <= 0)
            throw new BusinessRuleViolationException("Room capacity must be greater than zero.");

        if (pricePerSemester <= 0)
            throw new BusinessRuleViolationException("Price must be greater than zero.");

        RoomNumber = roomNumber;
        Capacity = capacity;
        PricePerSemester = pricePerSemester;
        HostelId = hostelId;
        IsAvailable = true;
    }

    public void AllocateStudent(Student student)
    {
        if (!IsAvailable)
            throw new BusinessRuleViolationException($"Room {RoomNumber} is not available.");

        if (CurrentOccupancy >= Capacity)
            throw new BusinessRuleViolationException(
                $"Room {RoomNumber} is full ({CurrentOccupancy}/{Capacity}).");

        CurrentOccupancy++;

        if (CurrentOccupancy >= Capacity)
            IsAvailable = false;

        SetUpdated();

        AddDomainEvent(new Events.AllocationCreatedEvent(
            0, // Will be populated after save
            student.Id,
            Id));
    }

    public void ReleaseStudent()
    {
        if (CurrentOccupancy <= 0)
            throw new BusinessRuleViolationException(
                $"Room {RoomNumber} has no occupants to release.");

        CurrentOccupancy--;

        IsAvailable = true;
        SetUpdated();
    }

    public int AvailableSpots => Capacity - CurrentOccupancy;

    protected override void SetUpdated()
    {
        Version = Guid.NewGuid();
        base.SetUpdated();
    }

    public void UpdatePrice(decimal newPrice)
    {
        if (newPrice <= 0)
            throw new BusinessRuleViolationException("Price must be greater than zero.");

        PricePerSemester = newPrice;
        SetUpdated();
    }

    public void UpdateDetails(string roomNumber, int capacity, decimal pricePerSemester)
    {
        if (string.IsNullOrWhiteSpace(roomNumber))
            throw new BusinessRuleViolationException("Room number cannot be empty.");
        if (capacity <= 0)
            throw new BusinessRuleViolationException("Capacity must be greater than zero.");
        if (pricePerSemester <= 0)
            throw new BusinessRuleViolationException("Price must be greater than zero.");
        if (capacity < CurrentOccupancy)
            throw new BusinessRuleViolationException($"Cannot set capacity {capacity} below current occupancy {CurrentOccupancy}.");

        RoomNumber = roomNumber;
        Capacity = capacity;
        PricePerSemester = pricePerSemester;
        // Re-evaluate availability based on new capacity
        IsAvailable = CurrentOccupancy < Capacity;
        SetUpdated();
    }
}
