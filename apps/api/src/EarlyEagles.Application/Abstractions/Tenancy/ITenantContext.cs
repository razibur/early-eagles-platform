namespace EarlyEagles.Application.Abstractions.Tenancy;

/// <summary>
/// Ambient tenant for the current request. Resolved by the tenant-resolution
/// middleware (P0-031.2) before any handler runs and consumed by the EF Core
/// global query filter (P0-032.2), Mediator behaviors, and any feature code
/// that needs to know which tenant it is operating on.
/// </summary>
/// <remarks>
/// Reader contract only — initialisation is intentionally a separate concern
/// behind <see cref="ITenantContextInitializer"/> so handler code cannot mutate
/// the ambient tenant mid-request.
/// </remarks>
public interface ITenantContext
{
    /// <summary>
    /// True once the tenant-resolution middleware has populated the context.
    /// </summary>
    bool IsResolved { get; }

    /// <summary>
    /// Tenant id for the current request. Throws
    /// <see cref="InvalidOperationException"/> when the context has not yet
    /// been resolved — handlers must run after the resolution middleware.
    /// </summary>
    Guid TenantId { get; }

    /// <summary>
    /// Tenant slug for the current request. Throws
    /// <see cref="InvalidOperationException"/> when the context has not yet
    /// been resolved.
    /// </summary>
    string Slug { get; }

    /// <summary>
    /// The Clerk organization the tenant is linked to, or <c>null</c> when it
    /// isn't linked (seed data). Session tokens count for this tenant only when
    /// their active organization is this one (P0-038.1). Throws
    /// <see cref="InvalidOperationException"/> when the context has not been
    /// resolved.
    /// </summary>
    string? ClerkOrganizationId { get; }
}
