namespace EarlyEagles.Application.Notifications;

/// <summary>
/// The stored subscription's id, and whether this call created it
/// (<c>false</c>: an existing row for the same browser was renewed or already matched).
/// </summary>
public sealed record SubscribeToPushResult(Guid SubscriptionId, bool Created);
