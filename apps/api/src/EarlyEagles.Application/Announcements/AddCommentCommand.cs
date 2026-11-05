using Mediator;

namespace EarlyEagles.Application.Announcements;

/// <summary>A member replies to a post (P1-012.2). Unknown here or in another club: 404.</summary>
public sealed record AddCommentCommand(Guid AnnouncementId, string AuthorClerkUserId, string Body) : ICommand<AddCommentResult>;
