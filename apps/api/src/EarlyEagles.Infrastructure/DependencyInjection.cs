using EarlyEagles.Application.Abstractions.Tenancy;
using EarlyEagles.Infrastructure.Persistence;
using EarlyEagles.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EarlyEagles.Infrastructure;

/// <summary>
/// Registers Infrastructure-layer services. Called from EarlyEagles.Api during
/// host composition so the API project never has to know about specific
/// implementations — composition is an infrastructure concern.
/// </summary>
public static class DependencyInjection
{
    // Placeholder connection until real per-environment configuration lands in
    // P0-033.4. AddDbContext / UseNpgsql never open a connection at registration,
    // so this keeps the host bootable (and the model buildable) without a live DB.
    // Also used by the design-time factory when no override env var is set.
    internal const string DefaultConnectionString =
        "Host=localhost;Port=5432;Database=earlyeagles;Username=postgres;Password=postgres";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Single scoped TenantContext exposed under both interfaces. Both reads
        // and the (single) initialise call within one request hit the same
        // instance; the next request gets a fresh one. Tenant-resolution
        // middleware (P0-031.2) calls Initialize; everyone else consumes the
        // read interface.
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantContextInitializer>(sp => sp.GetRequiredService<TenantContext>());

        // Placeholder tenant lookup — always returns null. Replaced by an
        // EF-backed implementation once a live database is wired (P0-033 / P0-034).
        // Scoped lifetime matches the future EF impl shape.
        services.AddScoped<ITenantLookup, NullTenantLookup>();

        // ApplicationDbContext holds the tenant-isolation global query filter
        // (P0-032.2). Scoped so it snapshots ITenantContext per request.
        var connectionString = configuration.GetConnectionString("Postgres") ?? DefaultConnectionString;
        services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));

        return services;
    }
}
