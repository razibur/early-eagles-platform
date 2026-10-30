using Mediator;

namespace EarlyEagles.Application.Tenants;

/// <summary>
/// Marks a tenant past due after a failed Stripe invoice payment (P0-043.4),
/// which raises <c>PaymentFailed</c> into the outbox. Returns <c>false</c>
/// when the tenant is already past due: nothing changes and nothing is raised.
/// </summary>
/// <remarks>
/// The caller sets the ambient tenant to <see cref="TenantId"/> first and has
/// checked that the tenant is active or past due, so the audit row lands in
/// that tenant's log and a failure here is an infrastructure fault worth a retry.
/// </remarks>
public sealed record RecordTenantPaymentFailureCommand(Guid TenantId, string StripeInvoiceId) : ICommand<bool>;
