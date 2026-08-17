namespace HostelSystem.Domain.Entities;

public abstract class Entity
{
    private readonly List<Events.IDomainEvent> _domainEvents = [];

    public int Id { get; protected set; }
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; protected set; }

    public IReadOnlyCollection<Events.IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(Events.IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void RemoveDomainEvent(Events.IDomainEvent domainEvent)
    {
        _domainEvents.Remove(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    protected virtual void SetUpdated()
    {
        UpdatedAt = DateTime.UtcNow;
    }
}
