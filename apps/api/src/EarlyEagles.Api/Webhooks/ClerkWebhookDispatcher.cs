using System.Text.Json;
using EarlyEagles.Application.Abstractions.Tenancy;
using EarlyEagles.Application.Tenants;
using EarlyEagles.Domain.Tenants;
using Mediator;

namespace EarlyEagles.Api.Webhooks;

/// <summary>
/// Turns a verified Clerk event into a command (P0-036.2.3 onward). Called by
/// <see cref="ClerkWebhookEndpoints"/> only after the signature check.
/// </summary>
/// <remarks>
/// A payload that can never succeed (a slug the platform can't use, a slug
/// another tenant owns, missing fields) is logged as an error and acknowledged:
/// Svix would otherwise retry it for days with the same result. Failures that
/// might pass on a retry (the database) throw, the endpoint answers 500, and
/// Svix retries.
/// </remarks>
public sealed class ClerkWebhookDispatcher
{
    private readonly IClerkTenantDirectory directory;
    private readonly ITenantContext tenantContext;
    private readonly ITenantContextInitializer tenantInitializer;
    private readonly ISender sender;
    private readonly ILogger<ClerkWebhookDispatcher> logger;

    public ClerkWebhookDispatcher(
        IClerkTenantDirectory directory,
        ITenantContext tenantContext,
        ITenantContextInitializer tenantInitializer,
        ISender sender,
        ILogger<ClerkWebhookDispatcher> logger)
    {
        this.directory = directory;
        this.tenantContext = tenantContext;
        this.tenantInitializer = tenantInitializer;
        this.sender = sender;
        this.logger = logger;
    }

    public Task DispatchAsync(string eventType, JsonElement data, string svixId, CancellationToken cancellationToken) =>
        eventType switch
        {
            "organization.created" => ProvisionTenantAsync(data, svixId, cancellationToken),

            // No handler yet (memberships and users: P0-036.3–.5). Acknowledged.
            _ => Task.CompletedTask,
        };

    private static string? ReadString(JsonElement data, string property) =>
        data.ValueKind == JsonValueKind.Object
        && data.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()
            : null;

    private async Task ProvisionTenantAsync(JsonElement data, string svixId, CancellationToken cancellationToken)
    {
        var organizationId = ReadString(data, "id");
        var slug = ReadString(data, "slug");
        var name = ReadString(data, "name");
        if (organizationId is null || slug is null || name is null)
        {
            ClerkWebhookLog.ProvisioningSkipped(logger, organizationId ?? "(none)", "missing id, slug or name", svixId);
            return;
        }

        if (!Tenant.IsValidSlug(slug))
        {
            ClerkWebhookLog.ProvisioningSkipped(logger, organizationId, $"slug '{slug}' is not a valid subdomain label", svixId);
            return;
        }

        if (!Tenant.IsValidDisplayName(name))
        {
            ClerkWebhookLog.ProvisioningSkipped(logger, organizationId, "name is blank or too long", svixId);
            return;
        }

        var entry = await directory.FindAsync(organizationId, slug, cancellationToken).ConfigureAwait(false);
        if (entry.TenantId is null && entry.SlugTakenByAnotherTenant)
        {
            ClerkWebhookLog.ProvisioningSkipped(logger, organizationId, $"slug '{slug}' belongs to another tenant", svixId);
            return;
        }

        // The command runs as the tenant it provisions, so its audit row is
        // that tenant's own (see ProvisionTenantCommand). The bare API host
        // leaves the context unresolved; a resolved one means the webhook was
        // sent to a tenant subdomain, which Clerk never does.
        if (tenantContext.IsResolved)
        {
            throw new InvalidOperationException("Clerk webhooks must target the bare API host, not a tenant subdomain.");
        }

        var tenantId = entry.TenantId ?? Guid.CreateVersion7();
        tenantInitializer.Initialize(tenantId, entry.Slug ?? slug);

        var result = await sender
            .Send(new ProvisionTenantCommand(tenantId, organizationId, slug, name), cancellationToken)
            .ConfigureAwait(false);
        ClerkWebhookLog.TenantProvisioned(logger, result, tenantId, organizationId, svixId);
    }
}
