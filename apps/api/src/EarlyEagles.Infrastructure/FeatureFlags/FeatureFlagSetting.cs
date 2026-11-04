namespace EarlyEagles.Infrastructure.FeatureFlags;

/// <summary>One feature's setting in <see cref="FeatureFlagOptions"/>.</summary>
public sealed class FeatureFlagSetting
{
    /// <summary>On for every tenant.</summary>
    public bool Enabled { get; set; }

    /// <summary>Tenant slugs that opted in while the feature is off for everyone else.</summary>
    public List<string> Tenants { get; } = [];
}
