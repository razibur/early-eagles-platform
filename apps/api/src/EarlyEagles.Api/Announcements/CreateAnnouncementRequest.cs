namespace EarlyEagles.Api.Announcements;

/// <summary>The body of <c>POST /announcements</c>.</summary>
public sealed record CreateAnnouncementRequest(string? Title, string? Body);
