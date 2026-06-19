namespace EarlyEagles.Domain.Primitives;

/// <summary>
/// Base class for aggregate roots. Adds a domain-event collection on top of
/// <see cref="Entity{TId}"/>. Aggregate roots accumulate events while the
/// transaction is open; the unit of work / outbox dispatcher reads and clears
/// them after persistence (see P0-049 / P0-050).
/// </summary>
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull
{
    private readonly List<IDomainEvent> domainEvents = [];

    protected AggregateRoot(TId id)
        : base(id)
    {
    }

    protected AggregateRoot()
    {
    }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => domainEvents.AsReadOnly();

    public void ClearDomainEvents()
    {
        domainEvents.Clear();
    }

    protected void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        domainEvents.Add(domainEvent);
    }
}
