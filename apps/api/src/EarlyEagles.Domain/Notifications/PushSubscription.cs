using EarlyEagles.Domain.Primitives;
using EarlyEagles.Domain.Tenants;

namespace EarlyEagles.Domain.Notifications;

/// <summary>
/// One browser's Web Push subscription for a member of a tenant (P0-068.3):
/// the push service <see cref="Endpoint"/> plus the two keys a sender needs to
/// encrypt for it. Scoped to the tenant the member subscribed in, so a club's
/// notifications only ever reach devices registered with that club.
/// </summary>
/// <remarks>
/// The endpoint identifies the browser, not the person: when someone else signs
/// in on the same browser and subscribes again, <see cref="Renew"/> moves the
/// row to them rather than keeping a second copy that would notify both.
/// </remarks>
public sealed class PushSubscription : Entity<Guid>, ITenantScoped
{
    public const int EndpointMaxLength = 2048;

    /// <summary>Generous for base64url keys: a P-256 public key is 87 characters, the auth secret 22.</summary>
    public const int KeyMaxLength = 256;

    private PushSubscription(Guid id, Guid tenantId, string clerkUserId, string endpoint, string p256dh, string auth, DateTimeOffset createdAt)
        : base(id)
    {
        TenantId = tenantId;
        ClerkUserId = clerkUserId;
        Endpoint = endpoint;
        P256dh = p256dh;
        Auth = auth;
        CreatedAt = createdAt;
    }

    // Parameterless ctor for EF Core materialisation.
    private PushSubscription()
    {
        ClerkUserId = string.Empty;
        Endpoint = string.Empty;
        P256dh = string.Empty;
        Auth = string.Empty;
    }

    public Guid TenantId { get; private set; }

    /// <summary>The member the browser currently belongs to.</summary>
    public string ClerkUserId { get; private set; }

    /// <summary>The push service URL the browser handed out (FCM, Mozilla, Apple, WNS).</summary>
    public string Endpoint { get; private set; }

    /// <summary>The browser's P-256 public key, base64url (<c>keys.p256dh</c>).</summary>
    public string P256dh { get; private set; }

    /// <summary>The browser's auth secret, base64url (<c>keys.auth</c>).</summary>
    public string Auth { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// True for an absolute <c>https</c> URL on a named host. The API will POST
    /// to this URL when it sends a notification, so IP literals, <c>localhost</c>
    /// and single-label hosts are refused: no push service uses them, and they
    /// would point the sender at the API's own network.
    /// </summary>
    public static bool IsValidEndpoint(string? endpoint)
    {
        if (string.IsNullOrEmpty(endpoint)
            || endpoint.Length > EndpointMaxLength
            || !Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        return uri.HostNameType == UriHostNameType.Dns
            && uri.Host.Contains('.', StringComparison.Ordinal)
            && !uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            && !uri.Host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>True for a non-empty base64url value (padding allowed) within <see cref="KeyMaxLength"/>.</summary>
    public static bool IsValidKey(string? key) =>
        !string.IsNullOrEmpty(key)
        && key.Length <= KeyMaxLength
        && key.TrimEnd('=').Length > 0
        && key.TrimEnd('=').All(c => char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_');

    public static PushSubscription Create(
        Guid id,
        Guid tenantId,
        string clerkUserId,
        string endpoint,
        string p256dh,
        string auth,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Push subscription id must not be empty.", nameof(id));
        }

        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant id must not be empty.", nameof(tenantId));
        }

        if (!IsValidEndpoint(endpoint))
        {
            throw new ArgumentException("Push endpoint must be an https URL on a named host.", nameof(endpoint));
        }

        ValidateOwner(clerkUserId, p256dh, auth);
        return new PushSubscription(id, tenantId, clerkUserId, endpoint, p256dh, auth, createdAt);
    }

    /// <summary>
    /// The same browser subscribed again: it may be signed in as someone else,
    /// or the push service may have issued new keys. Returns whether anything changed.
    /// </summary>
    public bool Renew(string clerkUserId, string p256dh, string auth)
    {
        ValidateOwner(clerkUserId, p256dh, auth);

        if (ClerkUserId == clerkUserId && P256dh == p256dh && Auth == auth)
        {
            return false;
        }

        ClerkUserId = clerkUserId;
        P256dh = p256dh;
        Auth = auth;
        return true;
    }

    private static void ValidateOwner(string clerkUserId, string p256dh, string auth)
    {
        if (!TenantMember.IsValidClerkUserId(clerkUserId))
        {
            throw new ArgumentException(
                $"Clerk user id must be non-blank and at most {TenantMember.ClerkUserIdMaxLength} characters.",
                nameof(clerkUserId));
        }

        if (!IsValidKey(p256dh))
        {
            throw new ArgumentException("p256dh must be a base64url key.", nameof(p256dh));
        }

        if (!IsValidKey(auth))
        {
            throw new ArgumentException("auth must be a base64url key.", nameof(auth));
        }
    }
}
