namespace EarlyEagles.Application.Tenants;

public enum ProvisionTenantResult
{
    /// <summary>A new tenant row was inserted.</summary>
    Created,

    /// <summary>The tenant existed; its name was brought in line with Clerk (or already was).</summary>
    Updated,
}
