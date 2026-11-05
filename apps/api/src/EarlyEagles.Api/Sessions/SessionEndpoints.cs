using EarlyEagles.Api.Authentication;
using EarlyEagles.Api.FeatureFlags;
using EarlyEagles.Application.FeatureFlags;
using EarlyEagles.Application.Sessions;
using Mediator;
using Microsoft.AspNetCore.Http.HttpResults;

namespace EarlyEagles.Api.Sessions;

/// <summary>
/// <c>POST /sessions</c> (P1-006): a tenant admin creates a session. No token
/// → 401, a member or another club's admin → 403, the sessions feature off for
/// this club → 404, an invalid session → 400.
/// </summary>
public static class SessionEndpoints
{
    public const string Route = "/sessions";

    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        routes
            .MapPost(Route, CreateSession)
            .RequireAuthorization(TenantPolicies.Admin)
            .RequireFeature(FeatureNames.Sessions)
            .WithName("CreateSession")
            .WithTags("Sessions");

        return routes;
    }

    private static async Task<Created<CreateSessionResponse>> CreateSession(
        CreateSessionRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        // A missing venue becomes an empty string, which the validator refuses
        // along with any other missing field.
        var result = await sender.Send(
            new CreateSessionCommand(request.Date, request.StartTime, request.Venue ?? string.Empty, request.FeeCents),
            cancellationToken);

        return TypedResults.Created($"{Route}/{result.SessionId}", new CreateSessionResponse(result.SessionId));
    }
}
