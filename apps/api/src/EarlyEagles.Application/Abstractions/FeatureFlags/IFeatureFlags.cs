namespace EarlyEagles.Application.Abstractions.FeatureFlags;

/// <summary>
/// Whether a feature is on for the ambient tenant (P1-031). Every new
/// user-facing capability ships behind a flag, off by default in production,
/// and tenants opt in. Today the answer comes from configuration; Flagsmith
/// replaces it behind this port (P0-075).
/// </summary>
public interface IFeatureFlags
{
    /// <summary>
    /// True when <paramref name="feature"/> is on for everyone, or for the
    /// ambient tenant. An unknown feature is off.
    /// </summary>
    ValueTask<bool> IsEnabledAsync(string feature, CancellationToken cancellationToken);
}
