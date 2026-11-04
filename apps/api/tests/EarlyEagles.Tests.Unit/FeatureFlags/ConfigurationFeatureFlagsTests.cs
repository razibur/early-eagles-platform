using EarlyEagles.Application.Abstractions.FeatureFlags;
using EarlyEagles.Application.Abstractions.Tenancy;
using EarlyEagles.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EarlyEagles.Tests.Unit.FeatureFlags;

// P1-031 — a feature is on for everyone, or for the tenants that opted in;
// anything not configured is off. Resolved through AddInfrastructure, so the
// binding and the registration are covered too.
public class ConfigurationFeatureFlagsTests
{
    [Fact]
    public async Task A_Feature_Not_In_Configuration_Is_Off()
    {
        Assert.False(await IsEnabledAsync([], "sessions", tenantSlug: "early-eagles"));
    }

    [Fact]
    public async Task A_Feature_Enabled_For_Everyone_Is_On_With_Or_Without_A_Tenant()
    {
        var settings = new Dictionary<string, string?> { ["FeatureFlags:Features:sessions:Enabled"] = "true" };

        Assert.True(await IsEnabledAsync(settings, "sessions", tenantSlug: "early-eagles"));
        Assert.True(await IsEnabledAsync(settings, "sessions", tenantSlug: null));
    }

    [Fact]
    public async Task An_Opted_In_Tenant_Sees_The_Feature_And_Others_Do_Not()
    {
        var settings = new Dictionary<string, string?>
        {
            ["FeatureFlags:Features:sessions:Enabled"] = "false",
            ["FeatureFlags:Features:sessions:Tenants:0"] = "early-eagles",
        };

        Assert.True(await IsEnabledAsync(settings, "sessions", tenantSlug: "early-eagles"));
        Assert.False(await IsEnabledAsync(settings, "sessions", tenantSlug: "other-club"));
        Assert.False(await IsEnabledAsync(settings, "sessions", tenantSlug: null));
    }

    [Fact]
    public async Task Feature_Names_Ignore_Case()
    {
        var settings = new Dictionary<string, string?> { ["FeatureFlags:Features:Sessions:Enabled"] = "true" };

        Assert.True(await IsEnabledAsync(settings, "sessions", tenantSlug: null));
    }

    [Fact]
    public void An_Opted_In_Tenant_That_Is_Not_A_Slug_Fails_Startup_Validation()
    {
        using var provider = Build(new() { ["FeatureFlags:Features:sessions:Tenants:0"] = "Early Eagles" });

        var exception = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
        Assert.Contains("FeatureFlags:Features", exception.Message, StringComparison.Ordinal);
    }

    private static async Task<bool> IsEnabledAsync(Dictionary<string, string?> settings, string feature, string? tenantSlug)
    {
        using var provider = Build(settings);
        using var scope = provider.CreateScope();
        if (tenantSlug is not null)
        {
            scope.ServiceProvider.GetRequiredService<ITenantContextInitializer>().Initialize(Guid.NewGuid(), tenantSlug);
        }

        return await scope.ServiceProvider.GetRequiredService<IFeatureFlags>().IsEnabledAsync(feature, CancellationToken.None);
    }

    private static ServiceProvider Build(Dictionary<string, string?> settings)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        return services.BuildServiceProvider();
    }
}
