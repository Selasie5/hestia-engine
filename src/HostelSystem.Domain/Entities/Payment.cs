using HostelSystem.Domain.Enums;
using HostelSystem.Domain.Exceptions;

namespace HostelSystem.Domain.Entities;

public class Payment : Entity
{
    public int StudentId { get; private set; }
    public Student Student { get; private set; } = null!;
    public int AllocationId { get; private set; }
    public RoomAllocation Allocation { get; private set; } = null!;
    public decimal Amount { get; private set; }
    public PaymentStatus Status { get; private set; }
    public string? TransactionReference { get; private set; }
    public string? PaymentMethod { get; private set; }
    public DateTime? PaidOn { get; private set; }
    public DateTime DueDate { get; private set; }

    private Payment() { } // EF Core

    public Payment(int studentId, int allocationId, decimal amount, DateTime dueDate)
    {
        if (amount <= 0)
            throw new BusinessRuleViolationException("Payment amount must be greater than zero.");

        StudentId = studentId;
        AllocationId = allocationId;
        Amount = amount;
        DueDate = dueDate;
        Status = PaymentStatus.Pending;
    }

    public void MarkCompleted(string transactionReference, string paymentMethod)
    {
        if (Status == PaymentStatus.Completed)
            throw new BusinessRuleViolationException(
                $"Payment with reference {TransactionReference} is already completed.");

        if (Status == PaymentStatus.Refunded)
            throw new BusinessRuleViolationException("Cannot complete a refunded payment.");

        if (string.IsNullOrWhiteSpace(transactionReference))
            throw new BusinessRuleViolationException("Transaction reference is required.");

        Status = PaymentStatus.Completed;
        TransactionReference = transactionReference;
        PaymentMethod = paymentMethod;
        PaidOn = DateTime.UtcNow;
        SetUpdated();

        AddDomainEvent(new Events.PaymentCompletedEvent(Id, transactionReference, Amount));
    }

    public void MarkFailed()
    {
        if (Status == PaymentStatus.Completed)
            throw new BusinessRuleViolationException("Cannot fail a completed payment.");

        Status = PaymentStatus.Failed;
        SetUpdated();
    }

    public void MarkRefunded()
    {
        if (Status != PaymentStatus.Completed)
            throw new BusinessRuleViolationException(
                $"Only completed payments can be refunded. Current status: {Status}.");

        Status = PaymentStatus.Refunded;
        SetUpdated();
    }

    public bool IsOverdue => Status == PaymentStatus.Pending && DateTime.UtcNow > DueDate;
}
