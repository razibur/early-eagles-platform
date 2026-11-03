using System.Security.Claims;
using EarlyEagles.Api.Authentication;
using EarlyEagles.Application.Notifications;
using Mediator;
using Microsoft.AspNetCore.Http.HttpResults;

namespace EarlyEagles.Api.Notifications;

/// <summary>
/// <c>POST /api/push/subscribe</c> (P0-068.3.2): the page posts its browser's
/// <c>PushSubscription.toJSON()</c> and the API stores it for the signed-in
/// member in this club. Members of the request's tenant only
/// (<see cref="TenantPolicies.Member"/>): no token → 401, another club's
/// session → 403. A malformed subscription → 400.
/// </summary>
public static class PushEndpoints
{
    public const string SubscribeRoute = "/api/push/subscribe";

    public static IEndpointRouteBuilder MapPushEndpoints(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        routes
            .MapPost(SubscribeRoute, Subscribe)
            .RequireAuthorization(TenantPolicies.Member)
            .WithName("SubscribeToPush")
            .WithTags("Notifications");

        return routes;
    }

    private static async Task<Ok<PushSubscribeResponse>> Subscribe(
        PushSubscribeRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        // Missing fields become empty strings, which the validator turns into
        // a 400 naming each one.
        var result = await sender.Send(
            new SubscribeToPushCommand(
                user.FindFirstValue(ClerkClaims.Subject) ?? string.Empty,
                request.Endpoint ?? string.Empty,
                request.Keys?.P256dh ?? string.Empty,
                request.Keys?.Auth ?? string.Empty),
            cancellationToken);

        return TypedResults.Ok(new PushSubscribeResponse(result.SubscriptionId));
    }
}
