namespace EarlyEagles.Application.Abstractions.Tenancy;

/// <summary>The tenant linked to a Clerk organization, as <see cref="IClerkTenantDirectory"/> reports it.</summary>
public sealed record ClerkLinkedTenant(Guid TenantId, string Slug);
