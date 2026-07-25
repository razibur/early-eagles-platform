using EarlyEagles.Domain.Primitives;

namespace EarlyEagles.Domain.Tenants;

/// <summary>
/// A Clerk user's membership in a single tenant. Identity is the composite
/// (TenantId, ClerkUserId); the same Clerk user is a separate
/// <see cref="TenantMember"/> in each tenant they belong to.
/// </summary>
/// <remarks>
/// P0-029.1 skeleton. Cross-aggregate invariants (e.g. "the last
/// TenantAdmin cannot be demoted") are application-layer concerns and are
/// pinned as documentation tests in P0-029.2.
/// </remarks>
public sealed class TenantMember : Entity<TenantMemberId>
{
    public const int ClerkUserIdMaxLength = 256;

    private TenantMember(TenantMemberId id, TenantMemberRole role)
        : base(id)
    {
        Role = role;
    }

    // Parameterless ctor for EF Core materialisation.
    private TenantMember()
    {
    }

    public Guid TenantId => Id.TenantId;

    public string ClerkUserId => Id.ClerkUserId;

    public TenantMemberRole Role { get; private set; }

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

        return new TenantMember(new TenantMemberId(tenantId, clerkUserId), role);
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
