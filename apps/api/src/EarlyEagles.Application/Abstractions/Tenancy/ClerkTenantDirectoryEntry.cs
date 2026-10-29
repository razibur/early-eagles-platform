namespace EarlyEagles.Application.Abstractions.Tenancy;

/// <summary>What <see cref="IClerkTenantDirectory"/> knows about an organization and a requested slug.</summary>
/// <param name="TenantId">The tenant linked to the organization, or <c>null</c> when there is none yet.</param>
/// <param name="Slug">That tenant's slug, when it exists.</param>
/// <param name="SlugTakenByAnotherTenant"><c>true</c> when a different tenant already uses the requested slug.</param>
public sealed record ClerkTenantDirectoryEntry(Guid? TenantId, string? Slug, bool SlugTakenByAnotherTenant);
