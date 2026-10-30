using EarlyEagles.Application.Abstractions.Tenancy;
using EarlyEagles.Infrastructure;
using EarlyEagles.Infrastructure.Tenancy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EarlyEagles.Tests.Unit.Tenancy;

// P0-030.1 — TenantContext implementation + DI registration shape. The
// per-request HTTP scope assertion belongs to the integration test in P0-030.2;
// here we cover the impl contract and the basic DI wiring at unit level.
public class TenantContextTests
{
    [Fact]
    public void Fresh_Context_Is_Not_Resolved()
    {
        var context = new TenantContext();

        Assert.False(context.IsResolved);
    }

    [Fact]
    public void Reading_TenantId_Before_Resolve_Throws()
    {
        var context = new TenantContext();

        Assert.Throws<InvalidOperationException>(() => context.TenantId);
    }

    [Fact]
    public void Reading_Slug_Before_Resolve_Throws()
    {
        var context = new TenantContext();

        Assert.Throws<InvalidOperationException>(() => context.Slug);
    }

    [Fact]
    public void Initialize_Populates_Both_Fields()
    {
        var context = new TenantContext();
        var tenantId = Guid.NewGuid();

        context.Initialize(tenantId, "early-eagles");

        Assert.True(context.IsResolved);
        Assert.Equal(tenantId, context.TenantId);
        Assert.Equal("early-eagles", context.Slug);
        Assert.Null(context.ClerkOrganizationId);
    }

    [Fact]
    public void Reading_ClerkOrganizationId_Before_Resolve_Throws()
    {
        var context = new TenantContext();

        Assert.Throws<InvalidOperationException>(() => context.ClerkOrganizationId);
    }

    [Fact]
    public void Initialize_Keeps_The_Clerk_Organization()
    {
        // P0-038.1 — role claims compare it with the token's active organization.
        var context = new TenantContext();

        context.Initialize(Guid.NewGuid(), "early-eagles", "org_2abcClub");

        Assert.Equal("org_2abcClub", context.ClerkOrganizationId);
    }

    [Fact]
    public void Double_Initialize_Throws()
    {
        var context = new TenantContext();
        context.Initialize(Guid.NewGuid(), "early-eagles");

        Assert.Throws<InvalidOperationException>(
            () => context.Initialize(Guid.NewGuid(), "other"));
    }

    [Fact]
    public void Initialize_Rejects_Empty_TenantId()
    {
        var context = new TenantContext();

        var ex = Assert.Throws<ArgumentException>(
            () => context.Initialize(Guid.Empty, "early-eagles"));

        Assert.Equal("tenantId", ex.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Initialize_Rejects_Empty_Or_Whitespace_Slug(string slug)
    {
        var context = new TenantContext();

        var ex = Assert.Throws<ArgumentException>(
            () => context.Initialize(Guid.NewGuid(), slug));

        Assert.Equal("slug", ex.ParamName);
    }

    [Fact]
    public void Initialize_Rejects_Null_Slug()
    {
        var context = new TenantContext();

        Assert.Throws<ArgumentNullException>(
            () => context.Initialize(Guid.NewGuid(), null!));
    }

    [Fact]
    public void AddInfrastructure_Registers_Both_Interfaces_As_Same_Scoped_Instance()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        var setter = scope.ServiceProvider.GetRequiredService<ITenantContextInitializer>();

        // Both interfaces resolve to the same backing object within one scope,
        // so a setter call is visible to the reader.
        Assert.Same(reader, setter);
        var tenantId = Guid.NewGuid();
        setter.Initialize(tenantId, "early-eagles");
        Assert.True(reader.IsResolved);
        Assert.Equal(tenantId, reader.TenantId);
    }

    [Fact]
    public void AddInfrastructure_Yields_A_Fresh_Instance_Per_Scope()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        ITenantContext first;
        ITenantContext second;
        using (var scope = provider.CreateScope())
        {
            first = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        }

        using (var scope = provider.CreateScope())
        {
            second = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        }

        Assert.NotSame(first, second);
    }
}
