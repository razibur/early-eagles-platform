using EarlyEagles.Domain.Primitives;

namespace EarlyEagles.Domain.Tenants;

/// <summary>
/// The tenant's Stripe subscription ended (P0-043.3): the tenant is back on the free plan and active. Raised by <see cref="Tenant.EndSubscription"/> once per subscription.
/// </summary>
public sealed record SubscriptionEnded(Guid TenantId, string StripeSubscriptionId) : IDomainEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
