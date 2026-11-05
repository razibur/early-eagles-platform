using Mediator;

namespace EarlyEagles.Application.Announcements;

/// <summary>
/// Removes a comment (P1-012.3): your own, or any as an admin. Someone else's as
/// a member: 403. Unknown here or in another club: 404.
/// </summary>
public sealed record DeleteCommentCommand(Guid AnnouncementId, Guid CommentId, string ActorClerkUserId, bool ActorIsAdmin) : ICommand;
