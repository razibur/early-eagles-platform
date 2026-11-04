using EarlyEagles.Application.Abstractions.FeatureFlags;
using EarlyEagles.Application.Abstractions.Tenancy;
using Microsoft.Extensions.Options;

namespace EarlyEagles.Infrastructure.FeatureFlags;

/// <summary>
/// <see cref="IFeatureFlags"/> over <see cref="FeatureFlagOptions"/> (P1-031).
/// Reads through <see cref="IOptionsMonitor{TOptions}"/>, so a changed
/// configuration source applies without a restart.
/// </summary>
public sealed class ConfigurationFeatureFlags : IFeatureFlags
{
    private readonly IOptionsMonitor<FeatureFlagOptions> options;
    private readonly ITenantContext tenantContext;

    public ConfigurationFeatureFlags(IOptionsMonitor<FeatureFlagOptions> options, ITenantContext tenantContext)
    {
        this.options = options;
        this.tenantContext = tenantContext;
    }

    public ValueTask<bool> IsEnabledAsync(string feature, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(feature);

        if (!options.CurrentValue.Features.TryGetValue(feature, out var setting))
        {
            return ValueTask.FromResult(false);
        }

        var enabled = setting.Enabled
            || (tenantContext.IsResolved && setting.Tenants.Contains(tenantContext.Slug, StringComparer.OrdinalIgnoreCase));
        return ValueTask.FromResult(enabled);
    }
}
