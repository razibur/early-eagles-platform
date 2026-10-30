using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;

namespace EarlyEagles.Api.Authentication;

/// <summary>
/// <c>GET /me</c> echoes the signed-in Clerk user (P0-035.2) and the
/// organization their session has active (P0-035.3), and their role in the
/// request's tenant (P0-038.1). The smallest endpoint
/// that requires a user: no token → 401.
/// </summary>
public static class MeEndpoints
{
    public const string Route = "/me";

    public static IEndpointRouteBuilder MapMeEndpoints(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        routes
            .MapGet(Route, Me)
            .RequireAuthorization()
            .WithName("Me")
            .WithTags("Diagnostics");

        return routes;
    }

    private static Ok<MeResponse> Me(ClaimsPrincipal user) =>
        TypedResults.Ok(new MeResponse(
            user.FindFirstValue(ClerkClaims.Subject)!,
            user.FindFirstValue(ClerkClaims.SessionId),
            ClerkActiveOrganization.From(user),
            user.FindFirstValue(TenantRoles.ClaimType)));
}
