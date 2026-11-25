namespace EarlyEagles.Api.Webhooks;

/// <summary>Which <c>customer.subscription.*</c> event <see cref="StripeWebhookDispatcher"/> is handling.</summary>
internal enum SubscriptionEventKind
{
    /// <summary><c>created</c>: sets the plan only (P0-045.2).</summary>
    Created = 0,

    /// <summary><c>updated</c>, <c>paused</c>, <c>resumed</c>: plan and status (P0-043.3, P0-045.2).</summary>
    Changed = 1,

    /// <summary><c>deleted</c>: ends the subscription.</summary>
    Deleted = 2,
}
