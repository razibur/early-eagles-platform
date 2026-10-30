using EarlyEagles.Domain.Tenants;

namespace EarlyEagles.Application.Abstractions.Tenancy;

/// <summary>A tenant as <see cref="IBillingTenantDirectory"/> reports it.</summary>
public sealed record BillingTenant(Guid TenantId, string Slug, TenantStatus Status, string? StripeCustomerId);
