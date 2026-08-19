using FluentAssertions;
using HostelSystem.Domain.Entities;
using HostelSystem.Domain.Enums;
using HostelSystem.Domain.Exceptions;

namespace HostelSystem.UnitTests.Domain;

public class PaymentTests
{
    // ── Constructor ──────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_ValidArgs_SetsPendingStatus()
    {
        var due = DateTime.UtcNow.AddDays(30);
        var payment = new Payment(studentId: 1, allocationId: 1, amount: 500m, dueDate: due);

        payment.Amount.Should().Be(500m);
        payment.Status.Should().Be(PaymentStatus.Pending);
        payment.DueDate.Should().BeCloseTo(due, TimeSpan.FromSeconds(1));
        payment.PaidOn.Should().BeNull();
        payment.TransactionReference.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_NonPositiveAmount_Throws(decimal amount)
    {
        Action act = () => new Payment(1, 1, amount, DateTime.UtcNow.AddDays(30));
        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*amount must be greater than zero*");
    }

    // ── MarkCompleted ─────────────────────────────────────────────────────────

    [Fact]
    public void MarkCompleted_FromPending_SetsCompletedStatus()
    {
        var payment = MakePayment();

        payment.MarkCompleted("TXN-001", "Mobile Money");

        payment.Status.Should().Be(PaymentStatus.Completed);
        payment.TransactionReference.Should().Be("TXN-001");
        payment.PaymentMethod.Should().Be("Mobile Money");
        payment.PaidOn.Should().NotBeNull();
    }

    [Fact]
    public void MarkCompleted_WhenAlreadyCompleted_Throws()
    {
        var payment = MakePayment();
        payment.MarkCompleted("TXN-001", "Mobile Money");

        Action act = () => payment.MarkCompleted("TXN-002", "Bank");
        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*already completed*");
    }

    [Fact]
    public void MarkCompleted_WhenRefunded_Throws()
    {
        var payment = MakePayment();
        payment.MarkCompleted("TXN-001", "Mobile Money");
        payment.MarkRefunded();

        Action act = () => payment.MarkCompleted("TXN-002", "Bank");
        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*Cannot complete a refunded payment*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void MarkCompleted_EmptyTransactionReference_Throws(string reference)
    {
        var payment = MakePayment();

        Action act = () => payment.MarkCompleted(reference, "Mobile Money");
        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*Transaction reference is required*");
    }

    // ── MarkFailed ────────────────────────────────────────────────────────────

    [Fact]
    public void MarkFailed_FromPending_SetsFailedStatus()
    {
        var payment = MakePayment();

        payment.MarkFailed();

        payment.Status.Should().Be(PaymentStatus.Failed);
    }

    [Fact]
    public void MarkFailed_WhenCompleted_Throws()
    {
        var payment = MakePayment();
        payment.MarkCompleted("TXN-001", "Mobile Money");

        Action act = () => payment.MarkFailed();
        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*Cannot fail a completed payment*");
    }

    // ── MarkRefunded ──────────────────────────────────────────────────────────

    [Fact]
    public void MarkRefunded_WhenCompleted_SetsRefundedStatus()
    {
        var payment = MakePayment();
        payment.MarkCompleted("TXN-001", "Mobile Money");

        payment.MarkRefunded();

        payment.Status.Should().Be(PaymentStatus.Refunded);
    }

    [Fact]
    public void MarkRefunded_WhenPending_Throws()
    {
        var payment = MakePayment();

        Action act = () => payment.MarkRefunded();
        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*Only completed payments can be refunded*");
    }

    // ── IsOverdue ─────────────────────────────────────────────────────────────

    [Fact]
    public void IsOverdue_WhenPendingAndPastDueDate_ReturnsTrue()
    {
        var payment = new Payment(1, 1, 500m, dueDate: DateTime.UtcNow.AddDays(-1));

        payment.IsOverdue.Should().BeTrue();
    }

    [Fact]
    public void IsOverdue_WhenPendingButNotYetDue_ReturnsFalse()
    {
        var payment = new Payment(1, 1, 500m, dueDate: DateTime.UtcNow.AddDays(10));

        payment.IsOverdue.Should().BeFalse();
    }

    [Fact]
    public void IsOverdue_WhenCompletedButPastDueDate_ReturnsFalse()
    {
        var payment = new Payment(1, 1, 500m, dueDate: DateTime.UtcNow.AddDays(-1));
        payment.MarkCompleted("TXN-001", "Mobile Money");

        payment.IsOverdue.Should().BeFalse();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Payment MakePayment() =>
        new(studentId: 1, allocationId: 1, amount: 500m, dueDate: DateTime.UtcNow.AddDays(30));
}
