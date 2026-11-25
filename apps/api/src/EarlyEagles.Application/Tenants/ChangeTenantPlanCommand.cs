using EarlyEagles.Domain.Tenants;
using Mediator;

namespace EarlyEagles.Application.Tenants;

/// <summary>
/// Moves a tenant to the plan its Stripe subscription's price sells
/// (P0-045.2), which raises <c>PlanChanged</c> into the outbox. Returns
/// <c>false</c> when nothing changed: already on that plan, or a subscription
/// the tenant has since replaced.
/// </summary>
/// <remarks>
/// The caller sets the ambient tenant to <see cref="TenantId"/> first and has
/// checked that the tenant isn't deactivated, so the audit row lands in that
/// tenant's log and a failure here is an infrastructure fault worth a retry.
/// </remarks>
public sealed record ChangeTenantPlanCommand(Guid TenantId, string StripeSubscriptionId, TenantPlan Plan) : ICommand<bool>;
