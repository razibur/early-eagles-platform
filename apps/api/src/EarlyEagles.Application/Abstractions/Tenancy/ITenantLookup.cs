namespace EarlyEagles.Application.Abstractions.Tenancy;

/// <summary>
/// Resolves a tenant slug to its identity for the tenant-resolution middleware
/// (P0-031.2). The real implementation reads from the EF-backed
/// <c>tenants</c> table (landing with P0-032.2 / P0-033.1); a
/// <c>NullTenantLookup</c> stub keeps the middleware wireable in the
/// meantime.
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
