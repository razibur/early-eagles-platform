using EarlyEagles.Application.Abstractions.Tenancy;

namespace EarlyEagles.Infrastructure.Tenancy;

/// <summary>
/// Placeholder <see cref="ITenantLookup"/> that never finds a tenant. Keeps
/// the tenant-resolution middleware wireable until the EF-backed implementation
/// lands with the DbContext (P0-032.2).
/// </summary>
public sealed class NullTenantLookup : ITenantLookup
{
    public Task<TenantLookupResult?> FindBySlugAsync(string slug, CancellationToken cancellationToken)
    {
        return Task.FromResult<TenantLookupResult?>(null);
    }
}
