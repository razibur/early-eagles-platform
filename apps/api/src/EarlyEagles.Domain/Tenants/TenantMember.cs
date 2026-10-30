using EarlyEagles.Domain.Primitives;

namespace EarlyEagles.Domain.Tenants;

/// <summary>
/// A Clerk user's membership in a single tenant. Identity is the composite
/// (TenantId, ClerkUserId); the same Clerk user is a separate
/// <see cref="TenantMember"/> in each tenant they belong to.
/// </summary>
/// <remarks>
/// P0-029.1 skeleton. Persistence (P0-032.2) maps <see cref="TenantId"/> and
/// <see cref="ClerkUserId"/> as the two-column composite key, so they are the
/// stored source of truth; <see cref="Entity{TId}.Id"/> is the value-object view
/// over them. EF Core binds the private constructor on materialisation (its
/// parameters match the mapped columns), which keeps <c>Id</c> correct for loaded
/// rows too. Cross-aggregate invariants (e.g. "the last TenantAdmin cannot be
/// demoted") are application-layer concerns and are pinned as documentation tests
/// in P0-029.2.
/// </remarks>
public sealed class TenantMember : Entity<TenantMemberId>, ITenantScoped
{
    public const int ClerkUserIdMaxLength = 256;

    // EF Core binds this constructor on materialisation: the parameter names
    // match the mapped columns (tenant_id, clerk_user_id, role). Domain callers
    // reach it through Create, which validates first.
    private TenantMember(Guid tenantId, string clerkUserId, TenantMemberRole role)
        : base(new TenantMemberId(tenantId, clerkUserId))
    {
        TenantId = tenantId;
        ClerkUserId = clerkUserId;
        Role = role;
    }

    /// <summary>
    /// Owning tenant and first half of the composite key. Satisfies
    /// <see cref="ITenantScoped"/> — the EF Core global query filter scopes members
    /// by this column.
    /// </summary>
    public Guid TenantId { get; }

    public string ClerkUserId { get; }

    public TenantMemberRole Role { get; private set; }

    /// <summary>
    /// True when <paramref name="clerkUserId"/> would pass <see cref="Create"/>'s
    /// check, so a webhook can pre-check a payload without catching exceptions.
    /// </summary>
    public static bool IsValidClerkUserId(string? clerkUserId) =>
        !string.IsNullOrWhiteSpace(clerkUserId) && clerkUserId.Length <= ClerkUserIdMaxLength;

    public static TenantMember Create(Guid tenantId, string clerkUserId, TenantMemberRole role)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant id must not be empty.", nameof(tenantId));
        }

        ValidateClerkUserId(clerkUserId);

        if (!Enum.IsDefined(role))
        {
            throw new ArgumentException($"Unknown role value: {role}.", nameof(role));
        }

        return new TenantMember(tenantId, clerkUserId, role);
    }

    public void ChangeRole(TenantMemberRole role)
    {
        if (!Enum.IsDefined(role))
        {
            throw new ArgumentException($"Unknown role value: {role}.", nameof(role));
        }

        Role = role;
    }

    private static void ValidateClerkUserId(string clerkUserId)
    {
        ArgumentNullException.ThrowIfNull(clerkUserId);

        if (string.IsNullOrWhiteSpace(clerkUserId))
        {
            throw new ArgumentException(
                "Clerk user id must not be empty or whitespace.",
                nameof(clerkUserId));
        }

        if (clerkUserId.Length > ClerkUserIdMaxLength)
        {
            throw new ArgumentException(
                $"Clerk user id must be at most {ClerkUserIdMaxLength} characters; got {clerkUserId.Length}.",
                nameof(clerkUserId));
        }
    }
}
