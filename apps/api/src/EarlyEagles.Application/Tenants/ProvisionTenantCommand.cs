using Mediator;

namespace EarlyEagles.Application.Tenants;

/// <summary>
/// Creates the tenant for a Clerk organization, or updates its name when the
/// tenant already exists (P0-036.2.2). Idempotent: Clerk delivers webhooks at
/// least once.
/// </summary>
/// <remarks>
/// The caller sets the ambient tenant to <see cref="TenantId"/> first: a new id
/// for a new organization, the linked tenant's id otherwise. The audit row then
/// lands in that tenant's own log (its first entry, for a new tenant), written
/// after the tenant row commits, which the audit table's foreign key requires.
/// The caller has also checked that the slug and name are usable, so a failure
/// here is an infrastructure fault worth a retry.
/// </remarks>
public sealed record ProvisionTenantCommand(
    Guid TenantId,
    string ClerkOrganizationId,
    string Slug,
    string DisplayName) : ICommand<ProvisionTenantResult>;
