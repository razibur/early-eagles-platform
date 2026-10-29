using EarlyEagles.Application.Abstractions.Tenancy;
using EarlyEagles.Application.Tenants;
using EarlyEagles.Infrastructure.Tenancy;
using EarlyEagles.Tests.Integration.Persistence;
using Mediator;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace EarlyEagles.Tests.Integration.Tenants;

// P0-036.2.2 — ProvisionTenantCommand through the real Mediator pipeline
// (audit, transaction) against Postgres, plus the platform-level directory the
// webhook consults first. Every test uses its own organization id and slug.
[Collection(PostgresCollectionDefinition.Name)]
public sealed class ProvisionTenantCommandTests : IDisposable
{
    private readonly PostgresFixture fixture;
    private readonly ProvisionFactory factory;

    public ProvisionTenantCommandTests(PostgresFixture fixture)
    {
        this.fixture = fixture;
        factory = new ProvisionFactory(fixture.ConnectionString);
    }

    public void Dispose() => factory.Dispose();

    [Fact]
    public async Task A_New_Organization_Creates_A_Linked_Tenant_And_Audits_It_In_Its_Own_Log()
    {
        var (orgId, slug) = NewOrganization();
        var tenantId = Guid.CreateVersion7();

        var result = await SendAsync(tenantId, new ProvisionTenantCommand(tenantId, orgId, slug, "  Provisioned Club  "));

        Assert.Equal(ProvisionTenantResult.Created, result);
        await using var db = fixture.CreateContext(new TenantContext());
        var tenant = await db.Tenants.IgnoreQueryFilters().SingleAsync(t => t.ClerkOrganizationId == orgId);
        Assert.Equal(tenantId, tenant.Id);
        Assert.Equal(slug, tenant.Slug);
        Assert.Equal("Provisioned Club", tenant.DisplayName);

        var audit = await db.AuditEvents.IgnoreQueryFilters().SingleAsync(e => e.TenantId == tenantId);
        Assert.Equal(nameof(ProvisionTenantCommand), audit.Action);
        Assert.True(audit.Success);
    }

    [Fact]
    public async Task A_Redelivery_Updates_The_Name_Without_A_Second_Tenant()
    {
        var (orgId, slug) = NewOrganization();
        var tenantId = Guid.CreateVersion7();
        await SendAsync(tenantId, new ProvisionTenantCommand(tenantId, orgId, slug, "First Name"));

        var result = await SendAsync(tenantId, new ProvisionTenantCommand(tenantId, orgId, "ignored-new-slug", "Second Name"));

        Assert.Equal(ProvisionTenantResult.Updated, result);
        await using var db = fixture.CreateContext(new TenantContext());
        var tenant = await db.Tenants.IgnoreQueryFilters().SingleAsync(t => t.ClerkOrganizationId == orgId);
        Assert.Equal("Second Name", tenant.DisplayName);
        Assert.Equal(slug, tenant.Slug);
        Assert.Equal(2, await db.AuditEvents.IgnoreQueryFilters().CountAsync(e => e.TenantId == tenantId));
    }

    [Fact]
    public async Task Provisioning_A_Tenant_Other_Than_The_Ambient_One_Is_Refused_And_Nothing_Is_Written()
    {
        var (orgId, slug) = NewOrganization();
        var tenantId = Guid.CreateVersion7();

        // Ambient tenant Alpha, command for a different tenant.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => SendAsync(PostgresFixture.AlphaTenantId, new ProvisionTenantCommand(tenantId, orgId, slug, "Wrong Ambient")));

        await using var db = fixture.CreateContext(new TenantContext());
        Assert.False(await db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.ClerkOrganizationId == orgId));
    }

    [Fact]
    public async Task The_Directory_Finds_The_Linked_Tenant_And_Reports_Slugs_Taken_By_Others()
    {
        var (orgId, slug) = NewOrganization();
        var tenantId = Guid.CreateVersion7();
        await SendAsync(tenantId, new ProvisionTenantCommand(tenantId, orgId, slug, "Directory Club"));

        using var scope = factory.Services.CreateScope();
        var directory = scope.ServiceProvider.GetRequiredService<IClerkTenantDirectory>();

        var same = await directory.FindAsync(orgId, slug, CancellationToken.None);
        Assert.Equal(new ClerkTenantDirectoryEntry(tenantId, slug, SlugTakenByAnotherTenant: false), same);

        var newcomer = await directory.FindAsync("org_newcomer_" + Guid.NewGuid().ToString("N"), slug, CancellationToken.None);
        Assert.Equal(new ClerkTenantDirectoryEntry(null, null, SlugTakenByAnotherTenant: true), newcomer);

        var alphaSlug = await directory.FindAsync("org_newcomer_" + Guid.NewGuid().ToString("N"), PostgresFixture.AlphaSlug, CancellationToken.None);
        Assert.True(alphaSlug.SlugTakenByAnotherTenant);
    }

    private static (string OrgId, string Slug) NewOrganization()
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        return ("org_" + suffix, "club-" + suffix);
    }

    private async Task<ProvisionTenantResult> SendAsync(Guid ambientTenantId, ProvisionTenantCommand command)
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextInitializer>().Initialize(ambientTenantId, "provisioning");
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(command);
    }

    private sealed class ProvisionFactory : WebApplicationFactory<Program>
    {
        private readonly string connectionString;

        public ProvisionFactory(string connectionString) => this.connectionString = connectionString;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<NpgsqlDataSource>();
                services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
            });
        }
    }
}
