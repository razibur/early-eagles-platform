using Mediator;

namespace EarlyEagles.Application.Notifications;

/// <summary>
/// Stores a browser's Web Push subscription for the signed-in member in the
/// ambient tenant (P0-068.3.2). Subscribing the same browser again renews its
/// row: new keys, or a different member now signed in on it.
/// </summary>
/// <remarks>
/// The fields are the browser's <c>PushSubscription.toJSON()</c>:
/// <c>endpoint</c>, <c>keys.p256dh</c>, <c>keys.auth</c>.
/// </remarks>
public sealed record SubscribeToPushCommand(
    string ClerkUserId,
    string Endpoint,
    string P256dh,
    string Auth) : ICommand<SubscribeToPushResult>;
