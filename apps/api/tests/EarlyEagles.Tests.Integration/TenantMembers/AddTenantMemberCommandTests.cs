using EarlyEagles.Application.Abstractions.Tenancy;
using EarlyEagles.Application.TenantMembers;
using EarlyEagles.Application.Tenants;
using EarlyEagles.Domain.Tenants;
using EarlyEagles.Infrastructure.Tenancy;
using EarlyEagles.Tests.Integration.Persistence;
using Mediator;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace EarlyEagles.Tests.Integration.TenantMembers;

// P0-036.3.1 — AddTenantMemberCommand through the real Mediator pipeline
// (audit, transaction) against Postgres. Each test provisions its own tenant,
// so its audit log holds only what the test wrote.
[Collection(PostgresCollectionDefinition.Name)]
public sealed class AddTenantMemberCommandTests : IDisposable
{
    private readonly PostgresFixture fixture;
    private readonly MemberFactory factory;

    public AddTenantMemberCommandTests(PostgresFixture fixture)
    {
        this.fixture = fixture;
        factory = new MemberFactory(fixture.ConnectionString);
    }

    public void Dispose() => factory.Dispose();

    [Fact]
    public async Task A_New_Member_Is_Added_And_Audited_In_The_Tenants_Log()
    {
        var tenantId = await ProvisionTenantAsync();

        var result = await SendAsync(tenantId, new AddTenantMemberCommand(tenantId, "user_new", TenantMemberRole.TenantAdmin));

        Assert.Equal(AddTenantMemberResult.Created, result);
        await using var db = fixture.CreateContext(new TenantContext());
        var member = await db.TenantMembers.IgnoreQueryFilters().SingleAsync(m => m.TenantId == tenantId);
        Assert.Equal("user_new", member.ClerkUserId);
        Assert.Equal(TenantMemberRole.TenantAdmin, member.Role);

        var audit = await db.AuditEvents.IgnoreQueryFilters()
            .SingleAsync(e => e.TenantId == tenantId && e.Action == nameof(AddTenantMemberCommand));
        Assert.True(audit.Success);
    }

    [Fact]
    public async Task A_Redelivery_Leaves_One_Member_And_Takes_Clerks_Role()
    {
        var tenantId = await ProvisionTenantAsync();
        await SendAsync(tenantId, new AddTenantMemberCommand(tenantId, "user_twice", TenantMemberRole.Member));

        var result = await SendAsync(tenantId, new AddTenantMemberCommand(tenantId, "user_twice", TenantMemberRole.TenantAdmin));

        Assert.Equal(AddTenantMemberResult.Updated, result);
        await using var db = fixture.CreateContext(new TenantContext());
        var member = await db.TenantMembers.IgnoreQueryFilters().SingleAsync(m => m.TenantId == tenantId);
        Assert.Equal(TenantMemberRole.TenantAdmin, member.Role);
    }

    [Fact]
    public async Task The_Same_Clerk_User_Is_A_Separate_Member_In_Each_Tenant()
    {
        var first = await ProvisionTenantAsync();
        var second = await ProvisionTenantAsync();

        await SendAsync(first, new AddTenantMemberCommand(first, "user_shared", TenantMemberRole.TenantAdmin));
        await SendAsync(second, new AddTenantMemberCommand(second, "user_shared", TenantMemberRole.Member));

        await using var db = fixture.CreateContext(new TenantContext());
        var roles = await db.TenantMembers.IgnoreQueryFilters()
            .Where(m => m.ClerkUserId == "user_shared" && (m.TenantId == first || m.TenantId == second))
            .ToDictionaryAsync(m => m.TenantId, m => m.Role);
        Assert.Equal(TenantMemberRole.TenantAdmin, roles[first]);
        Assert.Equal(TenantMemberRole.Member, roles[second]);
    }

    [Fact]
    public async Task Adding_A_Member_To_A_Tenant_Other_Than_The_Ambient_One_Is_Refused_And_Nothing_Is_Written()
    {
        var tenantId = await ProvisionTenantAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => SendAsync(PostgresFixture.AlphaTenantId, new AddTenantMemberCommand(tenantId, "user_wrong", TenantMemberRole.Member)));

        await using var db = fixture.CreateContext(new TenantContext());
        Assert.False(await db.TenantMembers.IgnoreQueryFilters().AnyAsync(m => m.ClerkUserId == "user_wrong"));
    }

    [Fact]
    public async Task The_Repository_Refuses_A_Context_Created_Before_The_Tenant_Was_Resolved()
    {
        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITenantMemberRepository>();
        scope.ServiceProvider.GetRequiredService<ITenantContextInitializer>().Initialize(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaSlug);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.FindAsync(PostgresFixture.AlphaTenantId, PostgresFixture.AlphaAdminUserId, CancellationToken.None));
    }

    private async Task<Guid> ProvisionTenantAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var tenantId = Guid.CreateVersion7();
        await SendAsync(tenantId, new ProvisionTenantCommand(tenantId, "org_" + suffix, "members-" + suffix, "Members Club"));
        return tenantId;
    }

    private async Task<TResult> SendAsync<TResult>(Guid ambientTenantId, ICommand<TResult> command)
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextInitializer>().Initialize(ambientTenantId, "members");
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(command);
    }

    private sealed class MemberFactory : WebApplicationFactory<Program>
    {
        private readonly string connectionString;

        public MemberFactory(string connectionString) => this.connectionString = connectionString;

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
