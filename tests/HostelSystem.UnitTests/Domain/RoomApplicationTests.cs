using FluentAssertions;
using HostelSystem.Domain.Entities;
using HostelSystem.Domain.Enums;
using HostelSystem.Domain.Exceptions;

namespace HostelSystem.UnitTests.Domain;

public class RoomApplicationTests
{
    // ── Constructor ──────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_SetsStatusToPending()
    {
        var app = new RoomApplication(studentId: 1, roomId: 2);

        app.Status.Should().Be(ApplicationStatus.Pending);
        app.ReviewedBy.Should().BeNull();
        app.ReviewedOn.Should().BeNull();
        app.RejectionReason.Should().BeNull();
    }

    [Fact]
    public void Constructor_AddsDomainEvent()
    {
        var app = new RoomApplication(studentId: 1, roomId: 2);
        app.DomainEvents.Should().ContainSingle();
    }

    // ── Approve ───────────────────────────────────────────────────────────────

    [Fact]
    public void Approve_FromPending_SetsStatusApproved()
    {
        var app = new RoomApplication(1, 2);

        app.Approve("admin@hostel.com");

        app.Status.Should().Be(ApplicationStatus.Approved);
        app.ReviewedBy.Should().Be("admin@hostel.com");
        app.ReviewedOn.Should().NotBeNull();
    }

    [Fact]
    public void Approve_WhenAlreadyApproved_Throws()
    {
        var app = new RoomApplication(1, 2);
        app.Approve("admin");

        Action act = () => app.Approve("admin");
        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*Only pending applications can be approved*");
    }

    [Fact]
    public void Approve_WhenRejected_Throws()
    {
        var app = new RoomApplication(1, 2);
        app.Reject("admin", "No space");

        Action act = () => app.Approve("admin");
        act.Should().Throw<BusinessRuleViolationException>();
    }

    // ── Reject ────────────────────────────────────────────────────────────────

    [Fact]
    public void Reject_FromPending_SetsStatusRejected()
    {
        var app = new RoomApplication(1, 2);

        app.Reject("admin@hostel.com", "No space available");

        app.Status.Should().Be(ApplicationStatus.Rejected);
        app.RejectionReason.Should().Be("No space available");
        app.ReviewedBy.Should().Be("admin@hostel.com");
    }

    [Fact]
    public void Reject_WhenAlreadyRejected_Throws()
    {
        var app = new RoomApplication(1, 2);
        app.Reject("admin", "reason");

        Action act = () => app.Reject("admin", "another reason");
        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*Only pending applications can be rejected*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Reject_EmptyReason_Throws(string reason)
    {
        var app = new RoomApplication(1, 2);

        Action act = () => app.Reject("admin", reason);
        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*Rejection reason is required*");
    }

    // ── Cancel ────────────────────────────────────────────────────────────────

    [Fact]
    public void Cancel_FromPending_Succeeds()
    {
        var app = new RoomApplication(1, 2);

        app.Cancel();

        app.Status.Should().Be(ApplicationStatus.Cancelled);
    }

    [Fact]
    public void Cancel_FromApproved_Succeeds()
    {
        var app = new RoomApplication(1, 2);
        app.Approve("admin");

        app.Cancel("Changed mind");

        app.Status.Should().Be(ApplicationStatus.Cancelled);
    }

    [Fact]
    public void Cancel_WhenRejected_Throws()
    {
        var app = new RoomApplication(1, 2);
        app.Reject("admin", "No space");

        Action act = () => app.Cancel();
        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*Cannot cancel an application with status*");
    }

    [Fact]
    public void Cancel_WhenAlreadyCancelled_Throws()
    {
        var app = new RoomApplication(1, 2);
        app.Cancel();

        Action act = () => app.Cancel();
        act.Should().Throw<BusinessRuleViolationException>();
    }
}
