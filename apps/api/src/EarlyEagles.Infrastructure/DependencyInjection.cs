using EarlyEagles.Application.Abstractions.Tenancy;
using EarlyEagles.Infrastructure.Tenancy;
using Microsoft.Extensions.DependencyInjection;

namespace EarlyEagles.Infrastructure;

/// <summary>
/// Registers Infrastructure-layer services. Called from EarlyEagles.Api during
/// host composition so the API project never has to know about specific
/// implementations — composition is an infrastructure concern.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Single scoped TenantContext exposed under both interfaces. Both reads
        // and the (single) initialise call within one request hit the same
        // instance; the next request gets a fresh one. Tenant-resolution
        // middleware (P0-031.2) calls Initialize; everyone else consumes the
        // read interface.
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantContextInitializer>(sp => sp.GetRequiredService<TenantContext>());

        // Placeholder tenant lookup — always returns null. Replaced by the
        // EF-backed implementation when the DbContext lands in P0-032.2.
        // Scoped lifetime matches the future EF impl shape.
        services.AddScoped<ITenantLookup, NullTenantLookup>();

        return services;
    }
}
