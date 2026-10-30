using System.Net;
using System.Net.Http.Json;
using EarlyEagles.Api.Authentication;
using EarlyEagles.Domain.Tenants;
using EarlyEagles.Infrastructure.Tenancy;
using EarlyEagles.Tests.Integration.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace EarlyEagles.Tests.Integration.Authentication;

// P0-038.1 — a session's role counts only in the club its active organization
// is linked to. Real tenants in Postgres, real subdomain resolution in
// Production, real token validation; only the signing key is local.
[Collection(PostgresCollectionDefinition.Name)]
public sealed class TenantRoleClaimTests
{
    private const string AlphaSlug = "roles-alpha";
    private const string BetaSlug = "roles-beta";
    private const string UnlinkedSlug = "roles-unlinked";
    private const string AlphaOrg = "org_roles_alpha";
    private const string BetaOrg = "org_roles_beta";

    private static readonly Guid AlphaId = new("03803800-0000-0000-0000-000000000001");
    private static readonly Guid BetaId = new("03803800-0000-0000-0000-000000000002");
    private static readonly Guid UnlinkedId = new("03803800-0000-0000-0000-000000000003");

    private static readonly SemaphoreSlim SeedLock = new(1, 1);

    private readonly PostgresFixture fixture;

    public TenantRoleClaimTests(PostgresFixture fixture) => this.fixture = fixture;

    [Theory]
    [InlineData("admin", TenantRoles.Admin)]
    [InlineData("org:admin", TenantRoles.Admin)]
    [InlineData("member", TenantRoles.Member)]
    [InlineData("coach", TenantRoles.Member)]
    public async Task On_Its_Own_Club_The_Session_Gets_Its_Role(string clerkRole, string expected)
    {
        var me = await MeAsync(AlphaSlug, ClerkTestTokens.ActiveOrganization(AlphaOrg, clerkRole));

        Assert.Equal(expected, me.TenantRole);
    }

    [Fact]
    public async Task An_Admin_Of_Another_Club_Gets_No_Role_Here()
    {
        var me = await MeAsync(BetaSlug, ClerkTestTokens.ActiveOrganization(AlphaOrg, "admin"));

        Assert.Null(me.TenantRole);
    }

    [Fact]
    public async Task A_Session_With_No_Active_Organization_Gets_No_Role()
    {
        var me = await MeAsync(AlphaSlug, extra: null);

        Assert.Null(me.TenantRole);
    }

    [Fact]
    public async Task A_Tenant_Not_Linked_To_Clerk_Gives_No_Role()
    {
        var me = await MeAsync(UnlinkedSlug, ClerkTestTokens.ActiveOrganization(AlphaOrg, "admin"));

        Assert.Null(me.TenantRole);
    }

    [Fact]
    public async Task A_Role_Claim_In_The_Token_Itself_Is_Not_Trusted()
    {
        // A token can carry any claim its issuer likes; the role must come from
        // the organization check, never straight from the token.
        var extra = new Dictionary<string, object> { [TenantRoles.ClaimType] = TenantRoles.Admin };

        var me = await MeAsync(BetaSlug, extra);

        Assert.Null(me.TenantRole);
    }

    private async Task<MeResponse> MeAsync(string slug, Dictionary<string, object>? extra)
    {
        await SeedOnceAsync();
        using var factory = new RolesFactory(fixture.ConnectionString);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Host = $"{slug}.earlyeagles.club";

        var token = ClerkTestTokens.Create("user_roles_check", "sess_roles_check", extra: extra);
        var response = await client.SendAsync(ClerkTestTokens.Get(MeEndpoints.Route, token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MeResponse>())!;
    }

    private async Task SeedOnceAsync()
    {
        await SeedLock.WaitAsync();
        try
        {
            await using var context = fixture.CreateContext(new TenantContext());
            if (await context.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == AlphaId))
            {
                return;
            }

            var alpha = Tenant.Create(AlphaId, AlphaSlug, "Roles Alpha");
            alpha.LinkClerkOrganization(AlphaOrg);
            var beta = Tenant.Create(BetaId, BetaSlug, "Roles Beta");
            beta.LinkClerkOrganization(BetaOrg);
            context.Tenants.AddRange(alpha, beta, Tenant.Create(UnlinkedId, UnlinkedSlug, "Roles Unlinked"));
            await context.SaveChangesAsync();
        }
        finally
        {
            SeedLock.Release();
        }
    }

    private sealed class RolesFactory : WebApplicationFactory<Program>
    {
        private readonly string connectionString;

        public RolesFactory(string connectionString) => this.connectionString = connectionString;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            ClerkTestTokens.UseTestIssuer(builder);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<NpgsqlDataSource>();
                services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
            });
        }
    }
}
