namespace EarlyEagles.Api.Notifications;

/// <summary>The stored subscription's id; the same id when the browser subscribes again.</summary>
public sealed record PushSubscribeResponse(Guid Id);
