namespace EarlyEagles.Domain.Primitives;

/// <summary>
/// Non-generic view of an aggregate's domain-event buffer. <see cref="AggregateRoot{TId}"/>
/// is generic in its id type, so infrastructure that has to sweep every tracked
/// aggregate — the outbox interceptor (P0-049.2) — cannot name it as a base type.
/// This interface is the seam it matches on instead.
/// </summary>
/// <remarks>
/// Deliberately the *whole* contract the sweep needs and nothing more: read the
/// buffer, then empty it once its contents are safely staged. Raising stays
/// protected on <see cref="AggregateRoot{TId}"/> — only the aggregate decides
/// what it announces.
/// </remarks>
public interface IHasDomainEvents
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}
