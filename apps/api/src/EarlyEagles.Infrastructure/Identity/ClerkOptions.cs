namespace EarlyEagles.Infrastructure.Identity;

/// <summary>
/// The <c>Clerk</c> configuration section (P0-036.1, P0-035.2). Secrets come
/// from Key Vault in Azure and user-secrets / <c>.env</c> on a laptop, never
/// from appsettings: set <c>Clerk__WebhookSigningSecret</c>. The issuer and
/// authorized parties are not secret.
/// </summary>
public sealed class ClerkOptions
{
    public const string SectionName = "Clerk";

    /// <summary>
    /// The endpoint's signing secret from the Clerk dashboard (Webhooks →
    /// endpoint → Signing Secret), <c>whsec_…</c>. Unset means the webhook
    /// endpoint answers 503 and processes nothing.
    /// </summary>
    public string? WebhookSigningSecret { get; set; }

    /// <summary>
    /// The instance's Frontend API URL, which Clerk puts in every session
    /// token's <c>iss</c> claim, e.g. <c>https://clerk.earlyeagles.club</c>
    /// (production) or <c>https://&lt;name&gt;.clerk.accounts.dev</c>
    /// (development). Signing keys are discovered from it. Unset means no
    /// bearer token is accepted: every request is anonymous.
    /// </summary>
    public string? Issuer { get; set; }

    /// <summary>
    /// Origins allowed in a session token's <c>azp</c> claim, e.g.
    /// <c>https://earlyeagles.club</c>. Clerk tokens carry no <c>aud</c>;
    /// <c>azp</c> is the browser origin that asked for the token and is what
    /// Clerk says to check instead (CSRF protection). Empty means every
    /// token is refused once <see cref="Issuer"/> is set.
    /// </summary>
    public IList<string> AuthorizedParties { get; } = [];

    /// <summary>True when <paramref name="issuer"/> is an absolute https URL with no path, query or trailing slash.</summary>
    public static bool IsValidIssuer(string issuer) =>
        Uri.TryCreate(issuer, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && uri.PathAndQuery == "/"
        && !issuer.EndsWith('/');

    /// <summary>True when <paramref name="origin"/> is an http(s) origin: scheme, host, optional port, nothing else.</summary>
    public static bool IsValidOrigin(string origin) =>
        Uri.TryCreate(origin, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
        && uri.PathAndQuery == "/"
        && !origin.EndsWith('/');
}
