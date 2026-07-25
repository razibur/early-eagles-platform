using EarlyEagles.Application.Abstractions.Tenancy;

namespace EarlyEagles.Infrastructure.Tenancy;

/// <summary>
/// Default scoped implementation of <see cref="ITenantContext"/> +
/// <see cref="ITenantContextInitializer"/>. Registered as a single scoped
/// instance — both interfaces resolve to the same object per request.
/// </summary>
public sealed class TenantContext : ITenantContext, ITenantContextInitializer
{
    private Guid tenantId;
    private string? slug;

    public bool IsResolved { get; private set; }

    public Guid TenantId =>
        IsResolved
            ? tenantId
            : throw new InvalidOperationException(
                "Tenant context has not been resolved for the current request.");

    public string Slug =>
        IsResolved
            ? slug!
            : throw new InvalidOperationException(
                "Tenant context has not been resolved for the current request.");

    public void Initialize(Guid tenantId, string slug)
    {
        if (IsResolved)
        {
            throw new InvalidOperationException(
                "Tenant context has already been resolved for the current request.");
        }

        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant id must not be empty.", nameof(tenantId));
        }

        ArgumentNullException.ThrowIfNull(slug);
        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new ArgumentException(
                "Tenant slug must not be empty or whitespace.",
                nameof(slug));
        }

        this.tenantId = tenantId;
        this.slug = slug;
        IsResolved = true;
    }
}
