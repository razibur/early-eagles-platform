using EarlyEagles.Domain.Tenants;
using Mediator;

namespace EarlyEagles.Application.TenantMembers;

/// <summary>
/// Adds a Clerk user to a tenant, or brings an existing member's role in line
/// with Clerk (P0-036.3.1). Idempotent: Clerk delivers webhooks at least once.
/// </summary>
/// <remarks>
/// The caller sets the ambient tenant to <see cref="TenantId"/> first, so the
/// member row and its audit row both belong to that tenant. Clerk is the
/// source of truth for the role, so a redelivery carrying a different role
/// overwrites ours. The profile (P0-036.4.2) seeds a new member only: a
/// redelivered membership event can be older than a later <c>user.updated</c>,
/// which owns profile changes from then on.
/// </remarks>
public sealed record AddTenantMemberCommand(
    Guid TenantId,
    string ClerkUserId,
    TenantMemberRole Role,
    string? DisplayName = null,
    string? Email = null,
    string? ImageUrl = null) : ICommand<AddTenantMemberResult>;
