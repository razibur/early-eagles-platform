namespace EarlyEagles.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Thrown by <see cref="TenantStampingInterceptor"/> when a write would place a
/// row outside the ambient tenant — an insert carrying another tenant's id, an
/// update or delete addressing another tenant's row, or an attempt to move a row
/// between tenants. It also covers the inverse gap: an insert with no tenant id
/// at all when there is no ambient tenant to stamp from.
/// </summary>
/// <remarks>
/// This is a bug signal, not a user error: the query filter means well-behaved
/// code cannot even load another tenant's row, so reaching this exception means
/// a handler constructed or attached an entity with the wrong tenant. It is
/// deliberately unhandled — the global exception handler turns it into a 500 —
/// because there is no safe way to continue a request that tried to cross a
/// tenant boundary.
/// </remarks>
public sealed class CrossTenantWriteException : InvalidOperationException
{
    public CrossTenantWriteException()
        : base("A write was rejected because it crossed a tenant boundary.")
    {
    }

    public CrossTenantWriteException(string message)
        : base(message)
    {
    }

    public CrossTenantWriteException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
