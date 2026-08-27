using EarlyEagles.Application.Abstractions.Tenancy;
using Microsoft.AspNetCore.Http.HttpResults;

namespace EarlyEagles.Api.Tenancy;

/// <summary>
/// Maps tenant-diagnostic endpoints.
/// <list type="bullet">
///   <item><c>/whoami</c> — echoes the tenant resolved by
///   <see cref="TenantResolutionMiddleware"/> for the current request. Its whole
///   purpose is to make subdomain → tenant resolution observable end-to-end
///   (P0-031.4). Being tenant-scoped, it is gated by the resolution middleware in
///   Production, so it is only reached with a resolved context there; outside
///   Production an unresolved request yields <c>400 Bad Request</c>.</item>
/// </list>
/// </summary>
public static class TenancyEndpoints
{
    public static IEndpointRouteBuilder MapTenancyEndpoints(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        routes
            .MapGet("/whoami", WhoAmI)
            .WithName("WhoAmI")
            .WithTags("Diagnostics");

        return routes;
    }

    private static Results<Ok<WhoAmIResponse>, ProblemHttpResult> WhoAmI(ITenantContext tenant)
    {
        if (!tenant.IsResolved)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Tenant not resolved",
                detail: "Requests must target a tenant subdomain, e.g. '<tenant>.earlyeagles.club'.");
        }

        return TypedResults.Ok(new WhoAmIResponse(tenant.TenantId, tenant.Slug));
    }
}
