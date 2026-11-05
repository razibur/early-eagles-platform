namespace EarlyEagles.Application.Announcements;

/// <summary>A reply as the feed shows it. <c>AuthorName</c> is <c>null</c> when Clerk has no name, or the author left.</summary>
public sealed record AnnouncementCommentItem(Guid Id, string AuthorClerkUserId, string? AuthorName, string Body, DateTimeOffset CreatedAt);
