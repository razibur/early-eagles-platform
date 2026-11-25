using EarlyEagles.Domain.Primitives;

namespace EarlyEagles.Domain.Tenants;

/// <summary>
/// Stripe reports the tenant's subscription active again after it went past due or was suspended (P0-043.3). Raised by <see cref="Tenant.ReactivateSubscription"/> on the move back to active, never for a redelivery.
/// </summary>
public sealed record SubscriptionReactivated(Guid TenantId, string StripeSubscriptionId) : IDomainEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
