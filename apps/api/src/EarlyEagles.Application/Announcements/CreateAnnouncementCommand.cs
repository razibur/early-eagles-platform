using Mediator;

namespace EarlyEagles.Application.Announcements;

/// <summary>An admin posts to the whole club (P1-012).</summary>
public sealed record CreateAnnouncementCommand(string AuthorClerkUserId, string Title, string Body) : ICommand<CreateAnnouncementResult>;
