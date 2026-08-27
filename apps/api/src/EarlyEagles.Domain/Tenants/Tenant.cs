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

        if (slug.Length < SlugMinLength || slug.Length > SlugMaxLength)
        {
            throw new ArgumentException(
                $"Tenant slug must be {SlugMinLength}–{SlugMaxLength} characters; got {slug.Length}.",
                nameof(slug));
        }

        if (!char.IsAsciiLetterLower(slug[0]))
        {
            throw new ArgumentException(
                "Tenant slug must start with a lowercase ASCII letter.",
                nameof(slug));
        }

        var last = slug[^1];
        if (!char.IsAsciiLetterLower(last) && !char.IsAsciiDigit(last))
        {
            throw new ArgumentException(
                "Tenant slug must end with a lowercase ASCII letter or digit.",
                nameof(slug));
        }

        for (var i = 0; i < slug.Length; i++)
        {
            var c = slug[i];
            var isAllowed = char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-';
            if (!isAllowed)
            {
                throw new ArgumentException(
                    $"Tenant slug may only contain lowercase ASCII letters, digits, and hyphens; got '{c}' at index {i}.",
                    nameof(slug));
            }

            if (c == '-' && i > 0 && slug[i - 1] == '-')
            {
                throw new ArgumentException(
                    "Tenant slug must not contain consecutive hyphens.",
                    nameof(slug));
            }
        }
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
