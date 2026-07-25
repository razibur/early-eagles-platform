namespace EarlyEagles.Infrastructure.Tenancy;

/// <summary>
/// Pure parser for tenant subdomains. Recognises <c>&lt;slug&gt;.earlyeagles.club</c>
/// in any environment and <c>&lt;slug&gt;.localhost</c> for local dev (the
/// dev hosts file pattern documented in P0-077.3). Format validation against
/// <see cref="EarlyEagles.Domain.Tenants.Tenant"/>'s slug rules is intentionally
/// out of scope — the tenant-resolution middleware (P0-031.2) does the actual
/// DB lookup, and an invalid format simply yields a lookup miss.
/// </summary>
public static class SubdomainExtractor
{
    // Base domains the platform serves tenants under. Order does not matter
    // today; if a longer base (e.g. "staging.earlyeagles.club") is added later
    // it should come first so the longest match wins.
    public static readonly IReadOnlyList<string> SupportedBaseDomains =
    [
        "earlyeagles.club",
        "localhost",
    ];

    public static bool TryExtract(string? host, out string slug)
    {
        slug = string.Empty;
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        var hostOnly = StripPort(host).Trim();
        if (hostOnly.Length == 0)
        {
            return false;
        }

        foreach (var baseDomain in SupportedBaseDomains)
        {
            var suffix = "." + baseDomain;
            if (!hostOnly.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var prefix = hostOnly[..^suffix.Length];
            if (prefix.Length == 0 || prefix.Contains('.'))
            {
                // Empty prefix means the bare base domain was hit; a dot in
                // the prefix means a nested subdomain we do not support.
                return false;
            }

            slug = prefix.ToLowerInvariant();
            return true;
        }

        return false;
    }

    private static string StripPort(string host)
    {
        // IPv6 hosts use brackets — strip anything after the closing bracket.
        if (host.StartsWith('['))
        {
            var close = host.IndexOf(']');
            return close < 0 ? host : host[..(close + 1)];
        }

        var colon = host.IndexOf(':');
        return colon < 0 ? host : host[..colon];
    }
}
