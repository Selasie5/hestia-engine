using HostelSystem.Domain.Enums;
using HostelSystem.Domain.Exceptions;

namespace HostelSystem.Domain.Entities;

public class RoomApplication : Entity
{
    public int StudentId { get; private set; }
    public Student Student { get; private set; } = null!;
    public int RoomId { get; private set; }
    public Room Room { get; private set; } = null!;
    public ApplicationStatus Status { get; private set; }
    public DateTime ApplicationDate { get; private set; }
    public DateTime? ReviewedOn { get; private set; }
    public string? ReviewedBy { get; private set; }
    public string? RejectionReason { get; private set; }
    public string? AdditionalNotes { get; private set; }

    private RoomApplication() { } // EF Core

    public RoomApplication(int studentId, int roomId, string? additionalNotes = null)
    {
        StudentId = studentId;
        RoomId = roomId;
        Status = ApplicationStatus.Pending;
        ApplicationDate = DateTime.UtcNow;
        AdditionalNotes = additionalNotes;

        AddDomainEvent(new Events.ApplicationSubmittedEvent(0, studentId, roomId));
    }

    public void Approve(string reviewedBy)
    {
        if (Status != ApplicationStatus.Pending)
            throw new BusinessRuleViolationException(
                $"Only pending applications can be approved. Current status: {Status}.");

        Status = ApplicationStatus.Approved;
        ReviewedBy = reviewedBy;
        ReviewedOn = DateTime.UtcNow;
        SetUpdated();

        AddDomainEvent(new Events.ApplicationApprovedEvent(Id, StudentId, RoomId));
    }

    public void Reject(string reviewedBy, string reason)
    {
        if (Status != ApplicationStatus.Pending)
            throw new BusinessRuleViolationException(
                $"Only pending applications can be rejected. Current status: {Status}.");

        if (string.IsNullOrWhiteSpace(reason))
            throw new BusinessRuleViolationException("Rejection reason is required.");

        Status = ApplicationStatus.Rejected;
        ReviewedBy = reviewedBy;
        ReviewedOn = DateTime.UtcNow;
        RejectionReason = reason;
        SetUpdated();

        AddDomainEvent(new Events.ApplicationRejectedEvent(Id, StudentId, reason));
    }

    public void Cancel(string? reason = null)
    {
        if (Status != ApplicationStatus.Pending && Status != ApplicationStatus.Approved)
            throw new BusinessRuleViolationException(
                $"Cannot cancel an application with status: {Status}.");

        Status = ApplicationStatus.Cancelled;
        SetUpdated();
    }
}
