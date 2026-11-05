using System.Security.Claims;
using EarlyEagles.Api.Authentication;
using EarlyEagles.Api.FeatureFlags;
using EarlyEagles.Application.Announcements;
using EarlyEagles.Application.FeatureFlags;
using Mediator;
using Microsoft.AspNetCore.Http.HttpResults;

namespace EarlyEagles.Api.Announcements;

/// <summary>
/// The club's announcements, behind the sessions flag with the rest of Phase 1:
/// 404 while it is off for this club, 401 without a token, 403 outside the
/// policy, 400 for invalid input.
/// </summary>
public static class AnnouncementEndpoints
{
    public const string Route = "/announcements";

    public static IEndpointRouteBuilder MapAnnouncementEndpoints(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        // P1-012: a tenant admin posts.
        routes
            .MapPost(Route, CreateAnnouncement)
            .RequireAuthorization(TenantPolicies.Admin)
            .RequireFeature(FeatureNames.Sessions)
            .WithName("CreateAnnouncement")
            .WithTags("Announcements");

        // P1-012.2: a member replies.
        routes
            .MapPost(Route + "/{id:guid}/comments", AddComment)
            .RequireAuthorization(TenantPolicies.Member)
            .RequireFeature(FeatureNames.Sessions)
            .WithName("AddAnnouncementComment")
            .WithTags("Announcements");

        // P1-012.3: the author, or an admin, deletes a comment.
        routes
            .MapDelete(Route + "/{id:guid}/comments/{commentId:guid}", DeleteComment)
            .RequireAuthorization(TenantPolicies.Member)
            .RequireFeature(FeatureNames.Sessions)
            .WithName("DeleteAnnouncementComment")
            .WithTags("Announcements");

        return routes;
    }

    private static string UserId(ClaimsPrincipal user) => user.FindFirstValue(ClerkClaims.Subject) ?? string.Empty;

    private static async Task<NoContent> DeleteComment(
        Guid id,
        Guid commentId,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new DeleteCommentCommand(id, commentId, UserId(user), user.HasClaim(TenantRoles.ClaimType, TenantRoles.Admin)),
            cancellationToken);

        return TypedResults.NoContent();
    }

    private static async Task<Created<CreatedResponse>> AddComment(
        Guid id,
        AddCommentRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new AddCommentCommand(id, UserId(user), request.Body ?? string.Empty), cancellationToken);
        return TypedResults.Created($"{Route}/{id}/comments/{result.CommentId}", new CreatedResponse(result.CommentId));
    }

    private static async Task<Created<CreatedResponse>> CreateAnnouncement(
        CreateAnnouncementRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateAnnouncementCommand(UserId(user), request.Title ?? string.Empty, request.Body ?? string.Empty),
            cancellationToken);

        return TypedResults.Created($"{Route}/{result.AnnouncementId}", new CreatedResponse(result.AnnouncementId));
    }
}
