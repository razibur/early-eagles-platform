using EarlyEagles.Domain.Primitives;

namespace EarlyEagles.Domain.Tenants;

/// <summary>
/// A tenant started a Stripe subscription (P0-043.2): its first checkout, or a
/// new subscription after a cancellation. Raised by
/// <see cref="Tenant.LinkStripeSubscription"/>, never for a redelivery.
/// </summary>
public sealed record SubscriptionStarted(Guid TenantId, string StripeCustomerId, string StripeSubscriptionId) : IDomainEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
