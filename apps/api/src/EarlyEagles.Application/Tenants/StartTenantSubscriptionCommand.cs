using Mediator;

namespace EarlyEagles.Application.Tenants;

/// <summary>
/// Records the Stripe customer and subscription a completed checkout created
/// for a tenant (P0-043.2), which raises <c>SubscriptionStarted</c> into the
/// outbox. Returns <c>false</c> for a redelivery: nothing changes and nothing
/// is raised.
/// </summary>
/// <remarks>
/// The caller sets the ambient tenant to <see cref="TenantId"/> first and has
/// checked that the tenant exists, isn't deactivated, and isn't billed to
/// another customer, so the audit row lands in that tenant's log and a failure
/// here is an infrastructure fault worth a retry.
/// </remarks>
public sealed record StartTenantSubscriptionCommand(
    Guid TenantId,
    string StripeCustomerId,
    string StripeSubscriptionId) : ICommand<bool>;
