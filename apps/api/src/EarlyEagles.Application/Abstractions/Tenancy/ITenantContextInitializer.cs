namespace EarlyEagles.Application.Abstractions.Tenancy;

/// <summary>
/// Setter half of the ambient tenant context. Resolved + called once per
/// request by the tenant-resolution middleware (P0-031.2). Kept separate from
/// <see cref="ITenantContext"/> so feature code cannot inject the setter and
/// mutate the ambient tenant mid-request.
/// </summary>
public interface ITenantContextInitializer
{
    /// <summary>
    /// Populate the request-scoped tenant context. Throws
    /// <see cref="InvalidOperationException"/> if the context has already been
    /// initialised for the current scope.
    /// </summary>
    void Initialize(Guid tenantId, string slug);
}
