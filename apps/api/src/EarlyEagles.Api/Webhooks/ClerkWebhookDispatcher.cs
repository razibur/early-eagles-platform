using System.Text.Json;
using EarlyEagles.Application.Abstractions.Tenancy;
using EarlyEagles.Application.TenantMembers;
using EarlyEagles.Application.Tenants;
using EarlyEagles.Domain.Tenants;
using EarlyEagles.Infrastructure.Identity;
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
/// Svix retries. An event that refers to an organization not provisioned yet
/// returns <see cref="ClerkWebhookOutcome.RetryLater"/>: Svix doesn't guarantee
/// order, and the creator's membership routinely races the organization.
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

    public Task<ClerkWebhookOutcome> DispatchAsync(string eventType, JsonElement data, string svixId, CancellationToken cancellationToken) =>
        eventType switch
        {
            "organization.created" => ProvisionTenantAsync(data, svixId, cancellationToken),
            "organizationMembership.created" => AddTenantMemberAsync(data, svixId, cancellationToken),

            // A new user belongs to no club yet, and profiles live on the
            // member row (P0-036.4): the membership event seeds it.
            "user.created" => Task.FromResult(ClerkWebhookOutcome.Acknowledged),

            // No handler yet (user.updated: P0-036.5.2). Acknowledged.
            _ => Task.FromResult(ClerkWebhookOutcome.Acknowledged),
        };

    private static JsonElement ReadObject(JsonElement data, string property) =>
        data.ValueKind == JsonValueKind.Object
        && data.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.Object
            ? value
            : default;

    private static string? ReadString(JsonElement data, string property) =>
        data.ValueKind == JsonValueKind.Object
        && data.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()
            : null;

    /// <summary>"First Last" from Clerk's name fields, or <c>null</c> when both are blank.</summary>
    private static string? ReadDisplayName(JsonElement data)
    {
        var name = $"{ReadString(data, "first_name")?.Trim()} {ReadString(data, "last_name")?.Trim()}".Trim();
        return name.Length > 0 ? name : null;
    }

    private async Task<ClerkWebhookOutcome> ProvisionTenantAsync(JsonElement data, string svixId, CancellationToken cancellationToken)
    {
        var organizationId = ReadString(data, "id");
        var slug = ReadString(data, "slug");
        var name = ReadString(data, "name");
        if (organizationId is null || slug is null || name is null)
        {
            ClerkWebhookLog.ProvisioningSkipped(logger, organizationId ?? "(none)", "missing id, slug or name", svixId);
            return ClerkWebhookOutcome.Acknowledged;
        }

        if (!Tenant.IsValidSlug(slug))
        {
            ClerkWebhookLog.ProvisioningSkipped(logger, organizationId, $"slug '{slug}' is not a valid subdomain label", svixId);
            return ClerkWebhookOutcome.Acknowledged;
        }

        if (!Tenant.IsValidDisplayName(name))
        {
            ClerkWebhookLog.ProvisioningSkipped(logger, organizationId, "name is blank or too long", svixId);
            return ClerkWebhookOutcome.Acknowledged;
        }

        var entry = await directory.FindAsync(organizationId, slug, cancellationToken).ConfigureAwait(false);
        if (entry.TenantId is null && entry.SlugTakenByAnotherTenant)
        {
            ClerkWebhookLog.ProvisioningSkipped(logger, organizationId, $"slug '{slug}' belongs to another tenant", svixId);
            return ClerkWebhookOutcome.Acknowledged;
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
        return ClerkWebhookOutcome.Acknowledged;
    }

    private async Task<ClerkWebhookOutcome> AddTenantMemberAsync(JsonElement data, string svixId, CancellationToken cancellationToken)
    {
        var organizationId = ReadString(ReadObject(data, "organization"), "id");
        var user = ReadObject(data, "public_user_data");
        var clerkUserId = ReadString(user, "user_id");
        if (organizationId is null || clerkUserId is null)
        {
            ClerkWebhookLog.MembershipSkipped(logger, organizationId ?? "(none)", clerkUserId ?? "(none)", "missing organization or user id", svixId);
            return ClerkWebhookOutcome.Acknowledged;
        }

        if (!TenantMember.IsValidClerkUserId(clerkUserId))
        {
            ClerkWebhookLog.MembershipSkipped(logger, organizationId, "(too long)", "user id is too long", svixId);
            return ClerkWebhookOutcome.Acknowledged;
        }

        var tenant = await directory.FindTenantAsync(organizationId, cancellationToken).ConfigureAwait(false);
        if (tenant is null)
        {
            ClerkWebhookLog.MembershipDeferred(logger, organizationId, clerkUserId, svixId);
            return ClerkWebhookOutcome.RetryLater;
        }

        // As for organizations: the command runs as the member's tenant.
        if (tenantContext.IsResolved)
        {
            throw new InvalidOperationException("Clerk webhooks must target the bare API host, not a tenant subdomain.");
        }

        tenantInitializer.Initialize(tenant.TenantId, tenant.Slug);

        var role = ClerkOrganizationRoles.ToTenantMemberRole(ReadString(data, "role"));
        var result = await sender
            .Send(
                new AddTenantMemberCommand(
                    tenant.TenantId,
                    clerkUserId,
                    role,
                    ReadDisplayName(user),
                    Email: ReadString(user, "identifier"),
                    ImageUrl: ReadString(user, "image_url")),
                cancellationToken)
            .ConfigureAwait(false);
        ClerkWebhookLog.MemberAdded(logger, result, clerkUserId, role, tenant.TenantId, svixId);
        return ClerkWebhookOutcome.Acknowledged;
    }
}
