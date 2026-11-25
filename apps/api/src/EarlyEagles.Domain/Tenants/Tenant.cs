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

    /// <summary>Stripe object ids (<c>cus_…</c>, <c>sub_…</c>) are at most 255 characters.</summary>
    public const int StripeIdMaxLength = 255;

    /// <summary>The longest IANA zone ids are around 32 characters.</summary>
    public const int TimeZoneIdMaxLength = 64;

    /// <summary>
    /// The zone a new tenant starts in: Tenant 0's. Clerk provisions tenants
    /// without a zone, and the onboarding wizard (P2-009) is what will ask.
    /// </summary>
    public const string DefaultTimeZoneId = "America/New_York";

    private Tenant(Guid id, string slug, string displayName)
        : base(id)
    {
        Slug = slug;
        DisplayName = displayName;
        TimeZoneId = DefaultTimeZoneId;
        Plan = TenantPlan.Free;
        Status = TenantStatus.Active;
    }

    // Parameterless ctor for EF Core materialisation.
    private Tenant()
    {
        Slug = string.Empty;
        DisplayName = string.Empty;
        TimeZoneId = string.Empty;
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

    /// <summary>
    /// The IANA zone the club plays in (P1-007.1). Session dates and start times
    /// are wall-clock values on this zone.
    /// </summary>
    public string TimeZoneId { get; private set; }

    public Branding? Branding { get; private set; }

    public TenantPlan Plan { get; private set; }

    public TenantStatus Status { get; private set; }

    /// <summary>
    /// The Clerk organization this tenant is (P0-036.2): Clerk org = tenant.
    /// <c>null</c> for a tenant created outside Clerk (seed data, tests).
    /// Set once; a tenant never moves to another organization.
    /// </summary>
    public string? ClerkOrganizationId { get; private set; }

    /// <summary>
    /// The Stripe customer that pays for this tenant's subscription (P0-045.1),
    /// <c>cus_…</c>. <c>null</c> until the tenant's first checkout. Set once:
    /// later subscriptions bill the same customer.
    /// </summary>
    public string? StripeCustomerId { get; private set; }

    /// <summary>
    /// The tenant's current Stripe subscription, <c>sub_…</c>. Replaced when the
    /// tenant subscribes again after a cancellation.
    /// </summary>
    public string? StripeSubscriptionId { get; private set; }

    /// <summary>True when <paramref name="slug"/> satisfies the subdomain rules <see cref="Create"/> enforces.</summary>
    public static bool IsValidSlug(string? slug) => slug is not null && SlugProblem(slug) is null;

    /// <summary>True when <paramref name="displayName"/> is non-blank and fits once trimmed.</summary>
    public static bool IsValidDisplayName(string? displayName) =>
        displayName is not null && displayName.Trim().Length is > 0 and <= DisplayNameMaxLength;

    /// <summary>True when <paramref name="timeZoneId"/> is an IANA zone this host knows. Windows ids are refused.</summary>
    public static bool IsValidTimeZoneId(string? timeZoneId) =>
        timeZoneId is { Length: > 0 and <= TimeZoneIdMaxLength }
        && TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var zone)
        && zone.HasIanaId;

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

    /// <summary>
    /// Records the Stripe customer and subscription a completed checkout
    /// created (P0-045.1) and raises <see cref="SubscriptionStarted"/>
    /// (P0-043.2). Returns <c>false</c>, raising nothing, when they are already recorded:
    /// Stripe delivers webhooks at least once. A different customer is a bug
    /// (one tenant, one customer); a different subscription replaces the old one.
    /// </summary>
    public bool LinkStripeSubscription(string customerId, string subscriptionId)
    {
        ValidateStripeId(customerId, nameof(customerId));
        ValidateStripeId(subscriptionId, nameof(subscriptionId));

        if (Status is TenantStatus.Deactivated)
        {
            throw new InvalidOperationException("Cannot link a subscription to a deactivated tenant.");
        }

        if (StripeCustomerId is not null && StripeCustomerId != customerId)
        {
            throw new InvalidOperationException(
                $"Tenant is already billed to Stripe customer '{StripeCustomerId}'.");
        }

        if (StripeCustomerId == customerId && StripeSubscriptionId == subscriptionId)
        {
            return false;
        }

        StripeCustomerId = customerId;
        StripeSubscriptionId = subscriptionId;
        RaiseDomainEvent(new SubscriptionStarted(Id, customerId, subscriptionId));
        return true;
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

    public void ChangeTimeZone(string timeZoneId)
    {
        if (!IsValidTimeZoneId(timeZoneId))
        {
            throw new ArgumentException($"Unknown IANA time zone: '{timeZoneId}'.", nameof(timeZoneId));
        }

        if (Status is TenantStatus.Deactivated)
        {
            throw new InvalidOperationException("Cannot change time zone of a deactivated tenant.");
        }

        TimeZoneId = timeZoneId;
    }

    /// <summary>
    /// <paramref name="instant"/> on the club's wall clock, the clock session
    /// dates and start times are on.
    /// </summary>
    public DateTime ToClubTime(DateTimeOffset instant) =>
        TimeZoneInfo.ConvertTime(instant, TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId)).DateTime;

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

    /// <summary>
    /// A Stripe invoice payment failed (P0-043.4): an active tenant becomes
    /// <see cref="TenantStatus.PastDue"/> and <see cref="PaymentFailed"/> is
    /// raised. Returns <c>false</c>, raising nothing, when the tenant is already
    /// past due: Stripe retries a failing invoice and redelivers webhooks.
    /// Suspended or deactivated tenants throw, as <see cref="MarkPastDue"/> does.
    /// </summary>
    public bool RecordPaymentFailure(string invoiceId)
    {
        ValidateStripeId(invoiceId, nameof(invoiceId));

        if (Status is TenantStatus.PastDue)
        {
            return false;
        }

        MarkPastDue();
        RaiseDomainEvent(new PaymentFailed(Id, invoiceId));
        return true;
    }

    /// <summary>
    /// Stripe reports the subscription active again (P0-043.3): a past-due or
    /// suspended tenant becomes <see cref="TenantStatus.Active"/> and
    /// <see cref="SubscriptionReactivated"/> is raised. Returns <c>false</c>,
    /// changing nothing, when the tenant is already active or
    /// <paramref name="subscriptionId"/> isn't its current subscription (an
    /// event about a subscription it has since replaced). Deactivated throws.
    /// </summary>
    public bool ReactivateSubscription(string subscriptionId)
    {
        if (!IsCurrentSubscription(subscriptionId) || Status is TenantStatus.Active)
        {
            return false;
        }

        Reactivate();
        RaiseDomainEvent(new SubscriptionReactivated(Id, subscriptionId));
        return true;
    }

    /// <summary>
    /// Stripe stopped collecting the subscription (P0-043.3): an active or
    /// past-due tenant is <see cref="TenantStatus.Suspended"/> and
    /// <see cref="SubscriptionSuspended"/> is raised. Returns <c>false</c>,
    /// changing nothing, when the tenant is already suspended or
    /// <paramref name="subscriptionId"/> isn't its current subscription.
    /// Deactivated throws.
    /// </summary>
    public bool SuspendSubscription(string subscriptionId)
    {
        if (!IsCurrentSubscription(subscriptionId) || Status is TenantStatus.Suspended)
        {
            return false;
        }

        Suspend();
        RaiseDomainEvent(new SubscriptionSuspended(Id, subscriptionId));
        return true;
    }

    /// <summary>
    /// The subscription ended (P0-043.3): the tenant forgets it, drops to
    /// <see cref="TenantPlan.Free"/>, owes nothing so is
    /// <see cref="TenantStatus.Active"/>, and <see cref="SubscriptionEnded"/>
    /// is raised. The customer stays recorded for a later checkout. Returns
    /// <c>false</c>, changing nothing, when <paramref name="subscriptionId"/>
    /// isn't the current subscription: already ended (a redelivery) or
    /// replaced. Deactivated throws.
    /// </summary>
    public bool EndSubscription(string subscriptionId)
    {
        if (!IsCurrentSubscription(subscriptionId))
        {
            return false;
        }

        StripeSubscriptionId = null;
        Plan = TenantPlan.Free;
        Reactivate();
        RaiseDomainEvent(new SubscriptionEnded(Id, subscriptionId));
        return true;
    }

    /// <summary>
    /// The subscription is billed on a price that belongs to
    /// <paramref name="plan"/> (P0-045.2): the tenant moves to that plan and
    /// <see cref="PlanChanged"/> is raised. Returns <c>false</c>, changing
    /// nothing, when the tenant is already on it or
    /// <paramref name="subscriptionId"/> isn't its current subscription.
    /// Deactivated throws.
    /// </summary>
    public bool ChangeSubscriptionPlan(string subscriptionId, TenantPlan plan)
    {
        if (!Enum.IsDefined(plan))
        {
            throw new ArgumentException($"Unknown plan value: {plan}.", nameof(plan));
        }

        if (!IsCurrentSubscription(subscriptionId) || Plan == plan)
        {
            return false;
        }

        var from = Plan;
        Plan = plan;
        RaiseDomainEvent(new PlanChanged(Id, subscriptionId, from, plan));
        return true;
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

    private static void ValidateStripeId(string id, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id, parameterName);
        if (id.Length > StripeIdMaxLength)
        {
            throw new ArgumentException(
                $"Stripe id must be at most {StripeIdMaxLength} characters; got {id.Length}.",
                parameterName);
        }
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

    private bool IsCurrentSubscription(string subscriptionId)
    {
        ValidateStripeId(subscriptionId, nameof(subscriptionId));

        if (Status is TenantStatus.Deactivated)
        {
            throw new InvalidOperationException("Cannot change the subscription of a deactivated tenant.");
        }

        return StripeSubscriptionId == subscriptionId;
    }
}
