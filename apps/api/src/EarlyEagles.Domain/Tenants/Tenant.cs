using EarlyEagles.Domain.Primitives;

namespace EarlyEagles.Domain.Tenants;

/// <summary>
/// A customer organisation on the platform — one badminton club. Every domain
/// entity outside of platform-level audit is scoped to a tenant. The slug is
/// the DNS label used in tenant subdomains (<c>&lt;slug&gt;.earlyeagles.club</c>),
/// so it follows RFC 1035 label rules: 2–63 ASCII chars, lowercase letter or
/// digit, hyphens allowed between but not at the edges.
/// </summary>
/// <remarks>
/// P0-028.1 skeleton + P0-028.2 branding + P0-028.3 plan / status state machine.
/// Comprehensive invariant tests land in P0-028.4.
/// </remarks>
public sealed class Tenant : AggregateRoot<Guid>, ITenantScoped
{
    public const int SlugMinLength = 2;
    public const int SlugMaxLength = 63;
    public const int DisplayNameMaxLength = 200;
    public const int ClerkOrganizationIdMaxLength = 64;

    private Tenant(Guid id, string slug, string displayName)
        : base(id)
    {
        Slug = slug;
        DisplayName = displayName;
        Plan = TenantPlan.Free;
        Status = TenantStatus.Active;
    }

    // Parameterless ctor for EF Core materialisation.
    private Tenant()
    {
        Slug = string.Empty;
        DisplayName = string.Empty;
    }

    /// <summary>
    /// Satisfies <see cref="ITenantScoped"/>. A tenant is scoped to itself, so its
    /// tenant id is its own <see cref="Entity{TId}.Id"/> — this lets the global
    /// query filter treat <see cref="Tenant"/> uniformly with every other
    /// tenant-scoped entity (a tenant sees only its own row). The pre-auth
    /// resolution lookup, which runs before any tenant context exists, bypasses
    /// the filter via <c>IgnoreQueryFilters</c> (P0-032.2).
    /// </summary>
    public Guid TenantId => Id;

    public string Slug { get; private set; }

    public string DisplayName { get; private set; }

    public Branding? Branding { get; private set; }

    public TenantPlan Plan { get; private set; }

    public TenantStatus Status { get; private set; }

    /// <summary>
    /// The Clerk organization this tenant is (P0-036.2): Clerk org = tenant.
    /// <c>null</c> for a tenant created outside Clerk (seed data, tests).
    /// Set once; a tenant never moves to another organization.
    /// </summary>
    public string? ClerkOrganizationId { get; private set; }

    /// <summary>True when <paramref name="slug"/> satisfies the subdomain rules <see cref="Create"/> enforces.</summary>
    public static bool IsValidSlug(string? slug) => slug is not null && SlugProblem(slug) is null;

    /// <summary>True when <paramref name="displayName"/> is non-blank and fits once trimmed.</summary>
    public static bool IsValidDisplayName(string? displayName) =>
        displayName is not null && displayName.Trim().Length is > 0 and <= DisplayNameMaxLength;

    public static Tenant Create(Guid id, string slug, string displayName)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Tenant id must not be empty.", nameof(id));
        }

        ValidateSlug(slug);
        var trimmedDisplayName = ValidateDisplayName(displayName);

        return new Tenant(id, slug, trimmedDisplayName);
    }

    /// <summary>
    /// Links the tenant to its Clerk organization. Idempotent for the same id
    /// (webhooks are delivered at least once); a different id is a bug.
    /// </summary>
    public void LinkClerkOrganization(string clerkOrganizationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clerkOrganizationId);
        if (clerkOrganizationId.Length > ClerkOrganizationIdMaxLength)
        {
            throw new ArgumentException(
                $"Clerk organization id must be at most {ClerkOrganizationIdMaxLength} characters; got {clerkOrganizationId.Length}.",
                nameof(clerkOrganizationId));
        }

        if (ClerkOrganizationId is not null && ClerkOrganizationId != clerkOrganizationId)
        {
            throw new InvalidOperationException(
                $"Tenant is already linked to Clerk organization '{ClerkOrganizationId}'.");
        }

        ClerkOrganizationId = clerkOrganizationId;
    }

    public void Rename(string displayName)
    {
        if (Status is TenantStatus.Deactivated)
        {
            throw new InvalidOperationException("Cannot rename a deactivated tenant.");
        }

        DisplayName = ValidateDisplayName(displayName);
    }

    public void SetBranding(Branding branding)
    {
        ArgumentNullException.ThrowIfNull(branding);

        if (Status is TenantStatus.Deactivated)
        {
            throw new InvalidOperationException("Cannot change branding of a deactivated tenant.");
        }

        Branding = branding;
    }

    public void ChangePlan(TenantPlan plan)
    {
        if (!Enum.IsDefined(plan))
        {
            throw new ArgumentException($"Unknown plan value: {plan}.", nameof(plan));
        }

        if (Status is TenantStatus.Deactivated)
        {
            throw new InvalidOperationException("Cannot change plan of a deactivated tenant.");
        }

        Plan = plan;
    }

    public void MarkPastDue()
    {
        if (Status is TenantStatus.PastDue)
        {
            return;
        }

        if (Status is not TenantStatus.Active)
        {
            throw new InvalidOperationException(
                $"Cannot mark past-due from status '{Status}'. Required: Active.");
        }

        Status = TenantStatus.PastDue;
    }

    public void Suspend()
    {
        if (Status is TenantStatus.Suspended)
        {
            return;
        }

        if (Status is not (TenantStatus.Active or TenantStatus.PastDue))
        {
            throw new InvalidOperationException(
                $"Cannot suspend from status '{Status}'. Required: Active or PastDue.");
        }

        Status = TenantStatus.Suspended;
    }

    public void Reactivate()
    {
        if (Status is TenantStatus.Active)
        {
            return;
        }

        if (Status is not (TenantStatus.PastDue or TenantStatus.Suspended))
        {
            throw new InvalidOperationException(
                $"Cannot reactivate from status '{Status}'. Required: PastDue or Suspended.");
        }

        Status = TenantStatus.Active;
    }

    public void Deactivate()
    {
        Status = TenantStatus.Deactivated;
    }

    private static void ValidateSlug(string slug)
    {
        ArgumentNullException.ThrowIfNull(slug);

        var problem = SlugProblem(slug);
        if (problem is not null)
        {
            throw new ArgumentException(problem, nameof(slug));
        }
    }

    /// <summary>Why <paramref name="slug"/> breaks the subdomain rules, or <c>null</c> when it doesn't.</summary>
    private static string? SlugProblem(string slug)
    {
        if (slug.Length < SlugMinLength || slug.Length > SlugMaxLength)
        {
            return $"Tenant slug must be {SlugMinLength}–{SlugMaxLength} characters; got {slug.Length}.";
        }

        if (!char.IsAsciiLetterLower(slug[0]))
        {
            return "Tenant slug must start with a lowercase ASCII letter.";
        }

        var last = slug[^1];
        if (!char.IsAsciiLetterLower(last) && !char.IsAsciiDigit(last))
        {
            return "Tenant slug must end with a lowercase ASCII letter or digit.";
        }

        for (var i = 0; i < slug.Length; i++)
        {
            var c = slug[i];
            var isAllowed = char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-';
            if (!isAllowed)
            {
                return $"Tenant slug may only contain lowercase ASCII letters, digits, and hyphens; got '{c}' at index {i}.";
            }

            if (c == '-' && i > 0 && slug[i - 1] == '-')
            {
                return "Tenant slug must not contain consecutive hyphens.";
            }
        }

        return null;
    }

    private static string ValidateDisplayName(string displayName)
    {
        ArgumentNullException.ThrowIfNull(displayName);

        var trimmed = displayName.Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException(
                "Tenant display name must not be empty or whitespace.",
                nameof(displayName));
        }

        if (trimmed.Length > DisplayNameMaxLength)
        {
            throw new ArgumentException(
                $"Tenant display name must be at most {DisplayNameMaxLength} characters; got {trimmed.Length}.",
                nameof(displayName));
        }

        return trimmed;
    }
}
