using Mediator;

namespace EarlyEagles.Application.Tenants;

/// <summary>
/// Applies a Stripe <c>customer.subscription.*</c> event to its tenant
/// (P0-043.3), which raises <c>SubscriptionReactivated</c>,
/// <c>SubscriptionSuspended</c> or <c>SubscriptionEnded</c> into the outbox.
/// Returns <c>false</c> when nothing changed: a redelivery, a status the tenant
/// already has, or a subscription the tenant has since replaced.
/// </summary>
/// <remarks>
/// The caller sets the ambient tenant to <see cref="TenantId"/> first and has
/// checked that the tenant isn't deactivated, so the audit row lands in that
/// tenant's log and a failure here is an infrastructure fault worth a retry.
/// </remarks>
public sealed record ChangeTenantSubscriptionCommand(Guid TenantId, string StripeSubscriptionId, TenantSubscriptionChange Change)
    : ICommand<bool>;
