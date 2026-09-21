using EarlyEagles.Domain.Primitives;

namespace EarlyEagles.Application.Abstractions.Messaging;

/// <summary>
/// Application-layer port for turning raised domain events into durable
/// messages. The Infrastructure implementation (P0-049.2) stages them as outbox
/// rows on the caller's unit of work, so the events and the state change that
/// produced them commit — or roll back — together.
/// </summary>
/// <remarks>
/// <para>
/// Dispatching is deliberately synchronous and returns nothing: it *stages*,
/// it does not publish. No message reaches the bus here and no database round
/// trip happens, so there is nothing to await and nothing to cancel. Delivery
/// is the outbox poller's job (P0-050), which runs after the transaction that
/// staged the row has committed — that separation is the whole point of the
/// outbox pattern.
/// </para>
/// <para>
/// The owning tenant is a parameter rather than something the implementation
/// reads from <c>ITenantContext</c>: the dispatcher is also driven from
/// platform work that runs with no ambient tenant (provisioning a tenant is the
/// first example), and the caller always knows which aggregate raised the
/// events.
/// </para>
/// </remarks>
public interface IDomainEventDispatcher
{
    /// <summary>
    /// Stages <paramref name="domainEvents"/> for publication on behalf of
    /// <paramref name="tenantId"/>. Nothing is persisted until the caller saves;
    /// nothing is published until the outbox is drained.
    /// </summary>
    void Dispatch(Guid tenantId, IReadOnlyCollection<IDomainEvent> domainEvents);
}
