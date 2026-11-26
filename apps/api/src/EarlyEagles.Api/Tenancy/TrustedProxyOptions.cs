namespace EarlyEagles.Api.Tenancy;

/// <summary>
/// The <c>TrustedProxy</c> configuration section (P0-071.4, ADR-0016). The web
/// app forwards the browser's <c>/api/*</c> calls here and says which club
/// address they were made on in <c>X-Forwarded-Host</c>. That header is only
/// believed when the request also carries <see cref="Key"/> in
/// <see cref="KeyHeader"/>; anyone can send <c>X-Forwarded-Host</c>, only
/// the web app knows the key.
/// <para>A secret: Key Vault in Azure, the Vercel project's environment for
/// the web app, user-secrets / <c>.env.local</c> on a laptop. Set
/// <c>TrustedProxy__Key</c>; never appsettings.</para>
/// </summary>
public sealed class TrustedProxyOptions
{
    public const string SectionName = "TrustedProxy";

    /// <summary>Header the web app puts the shared key in.</summary>
    public const string KeyHeader = "X-EE-Proxy-Key";

    /// <summary>Header carrying the address the browser called, set by the web app.</summary>
    public const string ForwardedHostHeader = "X-Forwarded-Host";

    /// <summary>Shortest key accepted, so it can't be guessed.</summary>
    public const int MinimumKeyLength = 32;

    /// <summary>
    /// The key shared with the web app. Unset means no forwarded host is ever
    /// believed: the API resolves the club from its own <c>Host</c> only.
    /// </summary>
    public string? Key { get; set; }

    /// <summary>True when <paramref name="key"/> is long enough and has no whitespace.</summary>
    public static bool IsValidKey(string key) =>
        key.Length >= MinimumKeyLength && !key.Any(char.IsWhiteSpace);
}
