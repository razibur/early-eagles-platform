using EarlyEagles.Domain.Tenants;

namespace EarlyEagles.Api.Tenancy;

/// <summary>
/// Wire representation of a <see cref="TenantMember"/> for the read endpoints.
/// Deliberately flat — no navigations, no domain events — so serialisation stays
/// stable regardless of aggregate shape changes.
/// </summary>
public sealed record TenantMemberResponse(Guid TenantId, string ClerkUserId, TenantMemberRole Role);
