namespace EarlyEagles.Application.Announcements;

/// <summary>A post as the feed shows it, with its replies oldest first.</summary>
public sealed record AnnouncementItem(
    Guid Id,
    string Title,
    string Body,
    string AuthorClerkUserId,
    string? AuthorName,
    DateTimeOffset CreatedAt,
    IReadOnlyList<AnnouncementCommentItem> Comments);
