namespace HostelSystem.Domain.Events;

public interface IDomainEvent
{
    DateTime OccurredOn { get; }
}

public abstract record DomainEvent : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record ApplicationSubmittedEvent(int ApplicationId, int StudentId, int RoomId) : DomainEvent;

public record ApplicationApprovedEvent(int ApplicationId, int StudentId, int RoomId) : DomainEvent;

public record ApplicationRejectedEvent(int ApplicationId, int StudentId, string Reason) : DomainEvent;

public record AllocationCreatedEvent(int AllocationId, int StudentId, int RoomId) : DomainEvent;

public record PaymentCompletedEvent(int PaymentId, string TransactionReference, decimal Amount) : DomainEvent;
