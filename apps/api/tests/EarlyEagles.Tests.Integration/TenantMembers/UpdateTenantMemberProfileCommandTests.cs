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

// P0-036.5.1 — UpdateTenantMemberProfileCommand through the real Mediator
// pipeline against Postgres, and the directory lookup that tells user.updated
// which tenants to patch. Each test provisions its own tenants.
[Collection(PostgresCollectionDefinition.Name)]
public sealed class UpdateTenantMemberProfileCommandTests : IDisposable
{
    private readonly PostgresFixture fixture;
    private readonly ProfileFactory factory;

    public UpdateTenantMemberProfileCommandTests(PostgresFixture fixture)
    {
        this.fixture = fixture;
        factory = new ProfileFactory(fixture.ConnectionString);
    }

    public void Dispose() => factory.Dispose();

    [Fact]
    public async Task The_Profile_Is_Replaced_And_Audited_In_The_Tenants_Log()
    {
        var tenantId = await ProvisionTenantAsync();
        await SendAsync(tenantId, new AddTenantMemberCommand(tenantId, "user_edit", TenantMemberRole.Member, "Old Name", "old@example.com", "https://img/old"));

        var result = await SendAsync(tenantId, new UpdateTenantMemberProfileCommand(tenantId, "user_edit", "New Name", "new@example.com", null));

        Assert.Equal(UpdateTenantMemberProfileResult.Updated, result);
        await using var db = fixture.CreateContext(new TenantContext());
        var member = await db.TenantMembers.IgnoreQueryFilters().SingleAsync(m => m.TenantId == tenantId);
        Assert.Equal("New Name", member.DisplayName);
        Assert.Equal("new@example.com", member.Email);
        Assert.Null(member.ImageUrl);
        Assert.Equal(TenantMemberRole.Member, member.Role);

        var audit = await db.AuditEvents.IgnoreQueryFilters()
            .SingleAsync(e => e.TenantId == tenantId && e.Action == nameof(UpdateTenantMemberProfileCommand));
        Assert.True(audit.Success);
    }

    [Fact]
    public async Task The_Same_Profile_Again_Is_Unchanged()
    {
        var tenantId = await ProvisionTenantAsync();
        await SendAsync(tenantId, new AddTenantMemberCommand(tenantId, "user_same", TenantMemberRole.Member, "Same Name", "same@example.com"));

        var result = await SendAsync(tenantId, new UpdateTenantMemberProfileCommand(tenantId, "user_same", "Same Name", "same@example.com", null));

        Assert.Equal(UpdateTenantMemberProfileResult.Unchanged, result);
    }

    [Fact]
    public async Task A_User_Who_Is_Not_A_Member_Is_Not_Added()
    {
        // The user may have left between the directory lookup and the command.
        var tenantId = await ProvisionTenantAsync();

        var result = await SendAsync(tenantId, new UpdateTenantMemberProfileCommand(tenantId, "user_gone", "Gone", null, null));

        Assert.Equal(UpdateTenantMemberProfileResult.NotAMember, result);
        await using var db = fixture.CreateContext(new TenantContext());
        Assert.False(await db.TenantMembers.IgnoreQueryFilters().AnyAsync(m => m.ClerkUserId == "user_gone"));
    }

    [Fact]
    public async Task Updating_A_Member_Of_A_Tenant_Other_Than_The_Ambient_One_Is_Refused()
    {
        var tenantId = await ProvisionTenantAsync();
        await SendAsync(tenantId, new AddTenantMemberCommand(tenantId, "user_guarded", TenantMemberRole.Member, "Kept Name"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => SendAsync(PostgresFixture.AlphaTenantId, new UpdateTenantMemberProfileCommand(tenantId, "user_guarded", "Hijacked", null, null)));

        await using var db = fixture.CreateContext(new TenantContext());
        var member = await db.TenantMembers.IgnoreQueryFilters().SingleAsync(m => m.TenantId == tenantId);
        Assert.Equal("Kept Name", member.DisplayName);
    }

    [Fact]
    public async Task The_Directory_Lists_Every_Tenant_The_User_Belongs_To()
    {
        var first = await ProvisionTenantAsync();
        var second = await ProvisionTenantAsync();
        var userId = "user_" + Guid.NewGuid().ToString("N")[..12];
        await SendAsync(first, new AddTenantMemberCommand(first, userId, TenantMemberRole.TenantAdmin));
        await SendAsync(second, new AddTenantMemberCommand(second, userId, TenantMemberRole.Member));

        using var scope = factory.Services.CreateScope();
        var directory = scope.ServiceProvider.GetRequiredService<IClerkTenantDirectory>();
        var tenants = await directory.FindTenantsOfUserAsync(userId, CancellationToken.None);
        var nobody = await directory.FindTenantsOfUserAsync("user_in_no_club", CancellationToken.None);

        Assert.Equal(new[] { first, second }.Order(), tenants.Select(t => t.TenantId).Order());
        Assert.All(tenants, t => Assert.StartsWith("profiles-", t.Slug, StringComparison.Ordinal));
        Assert.Empty(nobody);
    }

    private async Task<Guid> ProvisionTenantAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var tenantId = Guid.CreateVersion7();
        await SendAsync(tenantId, new ProvisionTenantCommand(tenantId, "org_" + suffix, "profiles-" + suffix, "Profiles Club"));
        return tenantId;
    }

    private async Task<TResult> SendAsync<TResult>(Guid ambientTenantId, ICommand<TResult> command)
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextInitializer>().Initialize(ambientTenantId, "profiles");
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(command);
    }

    private sealed class ProfileFactory : WebApplicationFactory<Program>
    {
        private readonly string connectionString;

        public ProfileFactory(string connectionString) => this.connectionString = connectionString;

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
