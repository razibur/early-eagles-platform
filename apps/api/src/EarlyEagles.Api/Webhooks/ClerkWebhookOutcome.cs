namespace EarlyEagles.Api.Webhooks;

/// <summary>What <see cref="ClerkWebhookDispatcher"/> tells the endpoint to answer.</summary>
public enum ClerkWebhookOutcome
{
    /// <summary>Handled, skipped as unusable, or no handler: 200, so Svix stops.</summary>
    Acknowledged,

    /// <summary>
    /// Depends on something not here yet (a membership for an organization not
    /// yet provisioned): non-2xx, so Svix retries with backoff.
    /// </summary>
    RetryLater,
}
