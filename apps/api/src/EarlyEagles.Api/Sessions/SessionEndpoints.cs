using System.Security.Claims;
using EarlyEagles.Api.Authentication;
using EarlyEagles.Api.FeatureFlags;
using EarlyEagles.Application.FeatureFlags;
using EarlyEagles.Application.Sessions;
using Mediator;
using Microsoft.AspNetCore.Http.HttpResults;

namespace EarlyEagles.Api.Sessions;

/// <summary>
/// The Sunday session endpoints, all behind the sessions flag: 404 while it is
/// off for this club, 401 without a token, 403 for anyone outside the policy
/// (another club's session always), 400 for invalid input.
/// </summary>
public static class SessionEndpoints
{
    public const string Route = "/sessions";

    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        // P1-006: a tenant admin creates a session.
        routes
            .MapPost(Route, CreateSession)
            .RequireAuthorization(TenantPolicies.Admin)
            .RequireFeature(FeatureNames.Sessions)
            .WithName("CreateSession")
            .WithTags("Sessions");

        // P1-005: a member lists one ISO week, ?week=2026-W45.
        routes
            .MapGet(Route, ListSessions)
            .RequireAuthorization(TenantPolicies.Member)
            .RequireFeature(FeatureNames.Sessions)
            .WithName("ListSessions")
            .WithTags("Sessions");

        // P1-005.2: a member reads one session, with their own RSVP and payment.
        routes
            .MapGet(Route + "/{id:guid}", GetSessionDetails)
            .RequireAuthorization(TenantPolicies.Member)
            .RequireFeature(FeatureNames.Sessions)
            .WithName("GetSessionDetails")
            .WithTags("Sessions");

        // P1-006.2: a tenant admin edits or cancels a session.
        routes
            .MapPut(Route + "/{id:guid}", UpdateSession)
            .RequireAuthorization(TenantPolicies.Admin)
            .RequireFeature(FeatureNames.Sessions)
            .WithName("UpdateSession")
            .WithTags("Sessions");

        routes
            .MapPost(Route + "/{id:guid}/cancel", CancelSession)
            .RequireAuthorization(TenantPolicies.Admin)
            .RequireFeature(FeatureNames.Sessions)
            .WithName("CancelSession")
            .WithTags("Sessions");

        // P1-007: a member answers for themselves.
        routes
            .MapPut(Route + "/{id:guid}/rsvp", SetRsvp)
            .RequireAuthorization(TenantPolicies.Member)
            .RequireFeature(FeatureNames.Sessions)
            .WithName("SetRsvp")
            .WithTags("Sessions");

        // P1-007.2: a tenant admin reads every member's answer for a session.
        routes
            .MapGet(Route + "/{id:guid}/rsvps", GetRsvpGrid)
            .RequireAuthorization(TenantPolicies.Admin)
            .RequireFeature(FeatureNames.Sessions)
            .WithName("GetRsvpGrid")
            .WithTags("Sessions");

        // P1-011: a member reads the club's leaderboard, ?period=year.
        routes
            .MapGet("/leaderboard", GetLeaderboard)
            .RequireAuthorization(TenantPolicies.Member)
            .RequireFeature(FeatureNames.Sessions)
            .WithName("GetLeaderboard")
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

    private static async Task<Ok<IReadOnlyList<SessionSummary>>> ListSessions(
        ISender sender,
        CancellationToken cancellationToken,
        string? week = null)
    {
        var sessions = await sender.Send(new ListSessionsQuery(week ?? string.Empty), cancellationToken);
        return TypedResults.Ok(sessions);
    }

    private static async Task<Ok<SessionDetails>> GetSessionDetails(
        Guid id,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var details = await sender.Send(
            new GetSessionDetailsQuery(id, user.FindFirstValue(ClerkClaims.Subject) ?? string.Empty),
            cancellationToken);

        return TypedResults.Ok(details);
    }

    private static async Task<NoContent> UpdateSession(
        Guid id,
        UpdateSessionRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new UpdateSessionCommand(id, request.Date, request.StartTime, request.Venue ?? string.Empty, request.FeeCents),
            cancellationToken);

        return TypedResults.NoContent();
    }

    private static async Task<NoContent> CancelSession(Guid id, ISender sender, CancellationToken cancellationToken)
    {
        await sender.Send(new CancelSessionCommand(id), cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<IReadOnlyList<LeaderboardRow>>> GetLeaderboard(
        ISender sender,
        CancellationToken cancellationToken,
        string? period = null)
    {
        var rows = await sender.Send(new GetLeaderboardQuery(period ?? string.Empty), cancellationToken);
        return TypedResults.Ok(rows);
    }

    private static async Task<NoContent> SetRsvp(
        Guid id,
        SetRsvpRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new SetRsvpCommand(id, user.FindFirstValue(ClerkClaims.Subject) ?? string.Empty, request.Response),
            cancellationToken);

        return TypedResults.NoContent();
    }

    private static async Task<Ok<IReadOnlyList<RsvpGridRow>>> GetRsvpGrid(Guid id, ISender sender, CancellationToken cancellationToken)
    {
        var grid = await sender.Send(new GetRsvpGridQuery(id), cancellationToken);
        return TypedResults.Ok(grid);
    }
}
