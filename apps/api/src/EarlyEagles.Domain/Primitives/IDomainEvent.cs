namespace EarlyEagles.Domain.Primitives;

/// <summary>
/// Marker interface for domain events. Domain events are recorded inside the
/// Domain layer and dispatched by the Application / Infrastructure layers
/// (see the outbox pattern in Phase 0 P0-049 / P0-050). The Domain library
/// intentionally has no MediatR / messaging dependency.
/// </summary>
public interface IDomainEvent
{
    Guid Id { get; }

    DateTimeOffset OccurredAt { get; }
}
