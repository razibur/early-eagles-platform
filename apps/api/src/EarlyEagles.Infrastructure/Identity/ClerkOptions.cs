namespace EarlyEagles.Infrastructure.Identity;

/// <summary>
/// The <c>Clerk</c> configuration section (P0-036.1). Secrets come from Key
/// Vault in Azure and user-secrets / <c>.env</c> on a laptop, never from
/// appsettings: set <c>Clerk__WebhookSigningSecret</c>.
/// </summary>
public sealed class ClerkOptions
{
    public const string SectionName = "Clerk";

    /// <summary>
    /// The endpoint's signing secret from the Clerk dashboard (Webhooks →
    /// endpoint → Signing Secret), <c>whsec_…</c>. Unset means the webhook
    /// endpoint answers 503 and processes nothing.
    /// </summary>
    public string? WebhookSigningSecret { get; set; }
}
