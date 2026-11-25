using EarlyEagles.Domain.Primitives;

namespace EarlyEagles.Domain.Tenants;

/// <summary>
/// Stripe gave up collecting the tenant's subscription (status <c>unpaid</c> or <c>paused</c>) and the tenant was suspended (P0-043.3). Raised by <see cref="Tenant.SuspendSubscription"/> on the move, never for a redelivery.
/// </summary>
public sealed record SubscriptionSuspended(Guid TenantId, string StripeSubscriptionId) : IDomainEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
