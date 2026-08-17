namespace HostelSystem.Domain.Entities;

public class Hostel : Entity
{
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public string Address { get; private set; }
    public bool IsActive { get; private set; }

    private readonly List<Room> _rooms = [];
    public IReadOnlyCollection<Room> Rooms => _rooms.AsReadOnly();

    private Hostel() { } // EF Core

    public Hostel(string name, string address, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new Exceptions.BusinessRuleViolationException("Hostel name cannot be empty.");

        if (string.IsNullOrWhiteSpace(address))
            throw new Exceptions.BusinessRuleViolationException("Hostel address cannot be empty.");

        Name = name;
        Address = address;
        Description = description;
        IsActive = true;
    }

    public void UpdateDetails(string name, string address, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new Exceptions.BusinessRuleViolationException("Hostel name cannot be empty.");

        if (string.IsNullOrWhiteSpace(address))
            throw new Exceptions.BusinessRuleViolationException("Hostel address cannot be empty.");

        Name = name;
        Address = address;
        Description = description;
        SetUpdated();
    }

    public void Deactivate()
    {
        if (!IsActive)
            throw new Exceptions.BusinessRuleViolationException("Hostel is already deactivated.");

        IsActive = false;
        SetUpdated();
    }

    public void Activate()
    {
        if (IsActive)
            throw new Exceptions.BusinessRuleViolationException("Hostel is already active.");

        IsActive = true;
        SetUpdated();
    }

    public int TotalCapacity => _rooms.Sum(r => r.Capacity);
    public int TotalOccupancy => _rooms.Sum(r => r.CurrentOccupancy);
    public int AvailableSpots => TotalCapacity - TotalOccupancy;
}
