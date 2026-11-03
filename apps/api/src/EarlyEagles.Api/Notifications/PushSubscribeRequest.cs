namespace EarlyEagles.Api.Notifications;

/// <summary>
/// The body of <c>POST /api/push/subscribe</c>: the browser's
/// <c>PushSubscription.toJSON()</c> as is. <c>expirationTime</c> is accepted
/// and ignored; push services in practice send <c>null</c>.
/// </summary>
public sealed record PushSubscribeRequest(string? Endpoint, PushSubscriptionKeys? Keys);
