using System.Net.Http.Headers;
using EarlyEagles.Domain.Sessions;
using EarlyEagles.Domain.Tenants;
using EarlyEagles.Infrastructure.Persistence;
using EarlyEagles.Infrastructure.Tenancy;
using EarlyEagles.Tests.Integration.Authentication;
using EarlyEagles.Tests.Integration.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace EarlyEagles.Tests.Integration.Sessions;

// Shared by the Phase 1 endpoint tests: two clubs of their own (Iota, Kappa), so
// they don't disturb the Alpha/Beta fixtures, and a host where the sessions
// feature is on for every club in Development and for Iota only in Production. The database lives for the whole
// collection, so tests pick dates or names no other test uses.
internal sealed class SessionApi : WebApplicationFactory<Program>
{
    public const string IotaSlug = "iota-phase1";
    public const string KappaSlug = "kappa-phase1";
    public const string IotaOrg = "org_iota_phase1";
    public const string KappaOrg = "org_kappa_phase1";

    public static readonly Guid IotaTenantId = new("aaaaaaaa-1005-0000-0000-000000000001");
    public static readonly Guid KappaTenantId = new("bbbbbbbb-1005-0000-0000-000000000001");

    private readonly string connectionString;
    private readonly string environment;

    public SessionApi(PostgresFixture fixture, string environment = "Development")
    {
        ArgumentNullException.ThrowIfNull(fixture);
        connectionString = fixture.ConnectionString;
        this.environment = environment;
    }

    public static async Task SeedClubsAsync(PostgresFixture fixture)
    {
        await using var db = fixture.CreateContext(new TenantContext());
        if (!await db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == IotaTenantId))
        {
            var iota = Tenant.Create(IotaTenantId, IotaSlug, "Iota Club");
            iota.LinkClerkOrganization(IotaOrg);
            var kappa = Tenant.Create(KappaTenantId, KappaSlug, "Kappa Club");
            kappa.LinkClerkOrganization(KappaOrg);
            db.Tenants.AddRange(iota, kappa);
            await db.SaveChangesAsync();
        }
    }

    public static ApplicationDbContext Iota(PostgresFixture fixture) => fixture.CreateContext(IotaTenantId, IotaSlug);

    public static ApplicationDbContext Kappa(PostgresFixture fixture) => fixture.CreateContext(KappaTenantId, KappaSlug);

    /// <summary>A new session in Iota, on a date the tests don't read back by week.</summary>
    public static async Task<Guid> SeedSessionAsync(PostgresFixture fixture, int feeCents = Session.DefaultFeeCents, bool cancelled = false)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        var session = Session.Create(Guid.CreateVersion7(), IotaTenantId, new DateOnly(2034, 1, 1), new TimeOnly(7, 0), "Seeded Hall", feeCents, DateTimeOffset.UtcNow);
        if (cancelled)
        {
            session.Cancel();
        }

        await using var db = Iota(fixture);
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return session.Id;
    }

    /// <summary>A client on Iota's subdomain, signed in to Iota unless another organization is named.</summary>
    public HttpClient Client(string role = "member", string userId = "user_phase1", string organization = IotaOrg, string slug = IotaSlug)
    {
        var client = Anonymous(slug);
        var token = ClerkTestTokens.Create(userId, "sess_" + userId, extra: ClerkTestTokens.ActiveOrganization(organization, role));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public HttpClient Anonymous(string slug = IotaSlug)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Host = $"{slug}.earlyeagles.club";
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.UseSetting("FeatureFlags:Features:sessions:Tenants:0", IotaSlug);

        // Stated here because appsettings.Development.json is gitignored, so CI has none.
        if (environment == "Development")
        {
            builder.UseSetting("FeatureFlags:Features:sessions:Enabled", "true");
        }

        ClerkTestTokens.UseTestIssuer(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<NpgsqlDataSource>();
            services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
        });
    }
}
