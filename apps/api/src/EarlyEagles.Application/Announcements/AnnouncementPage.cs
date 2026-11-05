namespace EarlyEagles.Application.Announcements;

/// <summary>One page of the feed. <c>NextCursor</c> is <c>null</c> on the last page.</summary>
public sealed record AnnouncementPage(IReadOnlyList<AnnouncementItem> Items, string? NextCursor);
