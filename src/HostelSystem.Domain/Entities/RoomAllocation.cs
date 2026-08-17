namespace HostelSystem.Domain.Entities;

public class RoomAllocation : Entity
{
    public int StudentId { get; private set; }
    public Student Student { get; private set; } = null!;
    public int RoomId { get; private set; }
    public Room Room { get; private set; } = null!;
    public int ApplicationId { get; private set; }
    public RoomApplication Application { get; private set; } = null!;
    public DateTime AllocationDate { get; private set; }
    public DateTime? CheckOutDate { get; private set; }
    public bool IsActive { get; private set; }

    private RoomAllocation() { } // EF Core

    public RoomAllocation(int studentId, int roomId, int applicationId)
    {
        if (studentId <= 0)
            throw new Exceptions.BusinessRuleViolationException("StudentId is required.");

        if (roomId <= 0)
            throw new Exceptions.BusinessRuleViolationException("RoomId is required.");

        StudentId = studentId;
        RoomId = roomId;
        ApplicationId = applicationId;
        AllocationDate = DateTime.UtcNow;
        IsActive = true;
    }

    public void CheckOut()
    {
        if (!IsActive)
            throw new Exceptions.BusinessRuleViolationException("This allocation is already inactive.");

        IsActive = false;
        CheckOutDate = DateTime.UtcNow;
        SetUpdated();
    }
}
