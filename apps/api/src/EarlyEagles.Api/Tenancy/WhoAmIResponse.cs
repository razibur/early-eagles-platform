namespace EarlyEagles.Api.Tenancy;

/// <summary>
/// Response body for <c>GET /whoami</c>. Echoes the tenant the resolution
/// middleware attached to the current request — the smallest possible proof
/// that subdomain → tenant resolution works end-to-end (P0-031.4).
/// </summary>
/// <param name="TenantId">Resolved tenant id for the request.</param>
/// <param name="Slug">Resolved tenant slug (matches the request subdomain).</param>
public sealed record WhoAmIResponse(Guid TenantId, string Slug);
