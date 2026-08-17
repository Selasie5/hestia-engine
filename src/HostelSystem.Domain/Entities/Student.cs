namespace HostelSystem.Domain.Entities;

public class Student : Entity
{
    public string UserId { get; private set; }
    public string StudentNumber { get; private set; }
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public Enums.Gender Gender { get; private set; }
    public string? PhoneNumber { get; private set; }
    public int? CurrentAllocationId { get; private set; }

    private readonly List<RoomApplication> _applications = [];
    public IReadOnlyCollection<RoomApplication> Applications => _applications.AsReadOnly();

    private Student() { } // EF Core

    public Student(string userId, string studentNumber, string firstName, string lastName, Enums.Gender gender)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new Exceptions.BusinessRuleViolationException("UserId cannot be empty.");

        if (string.IsNullOrWhiteSpace(studentNumber))
            throw new Exceptions.BusinessRuleViolationException("Student number cannot be empty.");

        if (string.IsNullOrWhiteSpace(firstName))
            throw new Exceptions.BusinessRuleViolationException("First name cannot be empty.");

        if (string.IsNullOrWhiteSpace(lastName))
            throw new Exceptions.BusinessRuleViolationException("Last name cannot be empty.");

        UserId = userId;
        StudentNumber = studentNumber;
        FirstName = firstName;
        LastName = lastName;
        Gender = gender;
    }

    public string FullName => $"{FirstName} {LastName}";

    public bool HasPendingApplication =>
        _applications.Any(a => a.Status == Enums.ApplicationStatus.Pending);

    public bool IsCurrentlyAllocated => CurrentAllocationId.HasValue;

    public void UpdateContactInfo(string? phoneNumber)
    {
        PhoneNumber = phoneNumber;
        SetUpdated();
    }
}
