using EarlyEagles.Application.TenantMembers;
using EarlyEagles.Application.Tenants;
using EarlyEagles.Domain.Tenants;
using EarlyEagles.Infrastructure.Identity;

namespace EarlyEagles.Api.Webhooks;

/// <summary>
/// Source-generated logging methods for <see cref="ClerkWebhookEndpoints"/>.
/// Separate file so StyleCop's one-type-per-file rule (SA1402) stays green.
/// </summary>
internal static partial class ClerkWebhookLog
{
    [LoggerMessage(EventId = 1400, Level = LogLevel.Information, Message = "Clerk webhook {EventType} received ({SvixId})")]
    public static partial void Received(ILogger logger, string eventType, string svixId);

    [LoggerMessage(EventId = 1401, Level = LogLevel.Warning, Message = "Clerk webhook rejected: {Reason} ({SvixId})")]
    public static partial void Rejected(ILogger logger, SvixVerificationResult reason, string svixId);

    [LoggerMessage(EventId = 1402, Level = LogLevel.Warning, Message = "Clerk webhook has a valid signature but no event type ({SvixId})")]
    public static partial void Malformed(ILogger logger, string svixId);

    [LoggerMessage(EventId = 1403, Level = LogLevel.Error, Message = "Clerk webhook received but Clerk:WebhookSigningSecret is not set")]
    public static partial void NotConfigured(ILogger logger);

    [LoggerMessage(
        EventId = 1404,
        Level = LogLevel.Information,
        Message = "Tenant {Result} for Clerk organization {OrganizationId}: {TenantId} ({SvixId})")]
    public static partial void TenantProvisioned(ILogger logger, ProvisionTenantResult result, Guid tenantId, string organizationId, string svixId);

    // Error, not Warning: the organization exists in Clerk with no tenant, and
    // nothing retries it. Someone has to look.
    [LoggerMessage(
        EventId = 1405,
        Level = LogLevel.Error,
        Message = "Clerk organization {OrganizationId} not provisioned: {Reason} ({SvixId})")]
    public static partial void ProvisioningSkipped(ILogger logger, string organizationId, string reason, string svixId);

    [LoggerMessage(
        EventId = 1406,
        Level = LogLevel.Information,
        Message = "Member {Result}: Clerk user {ClerkUserId} as {Role} in tenant {TenantId} ({SvixId})")]
    public static partial void MemberAdded(ILogger logger, AddTenantMemberResult result, string clerkUserId, TenantMemberRole role, Guid tenantId, string svixId);

    // Error: the person is in the Clerk organization but not in the club, and
    // nothing retries it.
    [LoggerMessage(
        EventId = 1407,
        Level = LogLevel.Error,
        Message = "Clerk membership of {ClerkUserId} in {OrganizationId} not recorded: {Reason} ({SvixId})")]
    public static partial void MembershipSkipped(ILogger logger, string organizationId, string clerkUserId, string reason, string svixId);

    // Warning, not Error: usually organization.created is a moment behind and
    // Svix's retry succeeds. Repeats mean the organization was never
    // provisioned (see 1405).
    [LoggerMessage(
        EventId = 1408,
        Level = LogLevel.Warning,
        Message = "Clerk membership of {ClerkUserId} in {OrganizationId} deferred: no tenant for the organization yet ({SvixId})")]
    public static partial void MembershipDeferred(ILogger logger, string organizationId, string clerkUserId, string svixId);
}
