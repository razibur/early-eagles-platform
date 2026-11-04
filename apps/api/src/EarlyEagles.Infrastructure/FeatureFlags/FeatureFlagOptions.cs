namespace EarlyEagles.Infrastructure.FeatureFlags;

/// <summary>
/// Feature flags from configuration (P1-031):
/// <code>
/// "FeatureFlags": { "Features": { "sessions": { "Enabled": false, "Tenants": [ "early-eagles" ] } } }
/// </code>
/// A feature is on when <see cref="FeatureFlagSetting.Enabled"/> is true, or
/// when the request's tenant slug is listed. A feature not listed at all is off.
/// </summary>
public sealed class FeatureFlagOptions
{
    public const string SectionName = "FeatureFlags";

    public Dictionary<string, FeatureFlagSetting> Features { get; } = new(StringComparer.OrdinalIgnoreCase);
}
