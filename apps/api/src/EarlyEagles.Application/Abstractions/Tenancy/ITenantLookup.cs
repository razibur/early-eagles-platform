namespace EarlyEagles.Application.Abstractions.Tenancy;

/// <summary>
/// Resolves a tenant slug to its identity for the tenant-resolution middleware
/// (P0-031.2). <c>EfTenantLookup</c> reads the <c>tenants</c> table (P0-031.5).
/// </summary>
public interface ITenantLookup
{
    /// <summary>
    /// Returns the tenant matching <paramref name="slug"/> or <c>null</c> if
    /// no tenant with that slug exists.
    /// </summary>
    Task<TenantLookupResult?> FindBySlugAsync(string slug, CancellationToken cancellationToken);
}

/// <summary>
/// Result of a tenant slug lookup. Carries the canonical slug as stored — the
/// caller may have provided a differently-cased input.
/// </summary>
public sealed record TenantLookupResult(Guid TenantId, string Slug);
