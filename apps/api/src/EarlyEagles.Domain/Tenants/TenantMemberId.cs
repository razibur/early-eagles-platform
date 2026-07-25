namespace EarlyEagles.Domain.Tenants;

/// <summary>
/// Composite identity for <see cref="TenantMember"/>: a Clerk user within a
/// specific tenant. The same Clerk user can be a member of many tenants —
/// each combination is a distinct <see cref="TenantMember"/>.
/// </summary>
public readonly record struct TenantMemberId(Guid TenantId, string ClerkUserId);
