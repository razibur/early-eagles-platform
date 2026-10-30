using EarlyEagles.Api.Authentication;
using EarlyEagles.Application.Abstractions.Tenancy;
using EarlyEagles.Application.Auditing;
using Mediator;
using Microsoft.AspNetCore.Http.HttpResults;

namespace EarlyEagles.Api.Auditing;

/// <summary>
/// <c>GET /audit-events</c> — the tenant's audit log, newest first, keyset
/// paged (P0-048.1). <c>?limit=</c> (1–100, default 50) and <c>?cursor=</c>
/// (the previous page's <c>nextCursor</c>).
/// </summary>
/// <remarks>
/// Tenant admins only (P0-048.2, <see cref="TenantPolicies.Admin"/>): no token
/// -> 401, a member or another club's admin -> 403. Mapped in every environment.
/// </remarks>
public static class AuditEventsEndpoints
{
    public static IEndpointRouteBuilder MapAuditEventsEndpoints(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        routes
            .MapGet("/audit-events", ListAuditEvents)
            .RequireAuthorization(TenantPolicies.Admin)
            .WithName("ListAuditEvents")
            .WithTags("Auditing");

        return routes;
    }

    private static async Task<Results<Ok<AuditEventPage>, ProblemHttpResult>> ListAuditEvents(
        ITenantContext tenant,
        ISender sender,
        CancellationToken cancellationToken,
        int limit = ListAuditEventsQuery.DefaultLimit,
        string? cursor = null)
    {
        if (!tenant.IsResolved)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Tenant not resolved",
                detail: "Requests must target a tenant subdomain, e.g. '<tenant>.earlyeagles.club'.");
        }

        // Invalid limit / cursor -> ValidationException -> 400 ProblemDetails
        // via ValidationExceptionHandler.
        var page = await sender.Send(new ListAuditEventsQuery(limit, cursor), cancellationToken);
        return TypedResults.Ok(page);
    }
}
