namespace EarlyEagles.Infrastructure.Billing;

/// <summary>
/// The <c>Stripe</c> configuration section (P0-043.1). Secrets come from Key
/// Vault in Azure and user-secrets / <c>.env</c> on a laptop, never from
/// appsettings: set <c>Stripe__WebhookSigningSecret</c>.
/// </summary>
public sealed class StripeOptions
{
    public const string SectionName = "Stripe";

    /// <summary>The prefix Stripe puts on every webhook signing secret.</summary>
    public const string WebhookSecretPrefix = "whsec_";

    /// <summary>
    /// The endpoint's signing secret, <c>whsec_…</c>: from the Stripe
    /// dashboard (Developers → Webhooks → endpoint → Signing secret), or the
    /// one <c>stripe listen</c> prints. Unset means the webhook endpoint
    /// answers 503 and processes nothing.
    /// </summary>
    public string? WebhookSigningSecret { get; set; }

    /// <summary>True when <paramref name="secret"/> is <c>whsec_</c> followed by something.</summary>
    public static bool IsValidWebhookSecret(string secret) =>
        secret.Length > WebhookSecretPrefix.Length
        && secret.StartsWith(WebhookSecretPrefix, StringComparison.Ordinal)
        && !secret.Any(char.IsWhiteSpace);
}
