namespace EarlyEagles.Api.Notifications;

/// <summary>The <c>keys</c> object of <c>PushSubscription.toJSON()</c>.</summary>
public sealed record PushSubscriptionKeys(string? P256dh, string? Auth);
