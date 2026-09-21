using System.Text.Json;
using EarlyEagles.Application.Abstractions.Messaging;
using EarlyEagles.Domain.Primitives;
using EarlyEagles.Infrastructure.Persistence;

namespace EarlyEagles.Infrastructure.Outbox;

/// <summary>
/// Writes raised domain events to the outbox (P0-049.2). Each event becomes an
/// <see cref="OutboxMessage"/> added to the same <see cref="ApplicationDbContext"/>
/// that carries the state change, so both land in one <c>SaveChanges</c> and
/// therefore one transaction: the row describing what happened cannot survive a
/// rollback of the thing that happened, and cannot go missing after a commit.
/// </summary>
/// <remarks>
/// <para>
/// Nothing is saved here. Staging on the tracked graph — rather than writing on
/// a separate connection the way <c>AuditService</c> deliberately does — is what
/// buys the atomicity. The audit log wants to outlive a rollback; the outbox
/// must not.
/// </para>
/// <para>
/// The outbox row reuses <see cref="IDomainEvent.Id"/> as its primary key.
/// That keeps the queued message traceable back to the event that raised it and
/// makes a double-dispatch of the same event instance fail loudly on the unique
/// key instead of quietly publishing twice.
/// </para>
/// <para>
/// Payloads are serialised with <see cref="JsonSerializerDefaults.Web"/>
/// (camelCase) against the event's runtime type, not the static
/// <see cref="IDomainEvent"/>, so derived properties actually make it into the
/// JSON. <see cref="OutboxMessage.EventType"/> stores the CLR full name without
/// assembly info, which is what the P0-050 publisher routes on.
/// </para>
/// </remarks>
public sealed class OutboxDomainEventDispatcher : IDomainEventDispatcher
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext context;

    public OutboxDomainEventDispatcher(ApplicationDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        this.context = context;
    }

    public void Dispatch(Guid tenantId, IReadOnlyCollection<IDomainEvent> domainEvents)
    {
        ArgumentNullException.ThrowIfNull(domainEvents);

        foreach (var domainEvent in domainEvents)
        {
            ArgumentNullException.ThrowIfNull(domainEvent);
            context.OutboxMessages.Add(ToOutboxMessage(tenantId, domainEvent));
        }
    }

    private static OutboxMessage ToOutboxMessage(Guid tenantId, IDomainEvent domainEvent)
    {
        var eventType = domainEvent.GetType();

        return OutboxMessage.Create(
            id: domainEvent.Id,
            tenantId: tenantId,
            eventType: eventType.FullName ?? eventType.Name,
            payload: JsonSerializer.Serialize(domainEvent, eventType, SerializerOptions),
            occurredAt: domainEvent.OccurredAt.ToUniversalTime());
    }
}
