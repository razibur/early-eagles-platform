using EarlyEagles.Domain.Primitives;

namespace EarlyEagles.Domain.Tenants;

/// <summary>
/// The tenant's Stripe subscription moved to a price on another plan (P0-045.2).
/// Raised by <see cref="Tenant.ChangeSubscriptionPlan"/> when the plan actually
/// changes, never for a redelivery.
/// </summary>
public sealed record PlanChanged(Guid TenantId, string StripeSubscriptionId, TenantPlan From, TenantPlan To) : IDomainEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
