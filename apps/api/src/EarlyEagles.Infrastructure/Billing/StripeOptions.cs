using EarlyEagles.Domain.Tenants;

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

    /// <summary>The prefix Stripe puts on every price id.</summary>
    public const string PriceIdPrefix = "price_";

    /// <summary>
    /// The endpoint's signing secret, <c>whsec_…</c>: from the Stripe
    /// dashboard (Developers → Webhooks → endpoint → Signing secret), or the
    /// one <c>stripe listen</c> prints. Unset means the webhook endpoint
    /// answers 503 and processes nothing.
    /// </summary>
    public string? WebhookSigningSecret { get; set; }

    /// <summary>
    /// Which plan each Stripe price sells (P0-045.2), keyed by price id, e.g.
    /// <c>Stripe__Prices__price_1Abc=Pro</c>. Price ids are not secret; they
    /// come from the dashboard products (P0-042.*). A price missing here
    /// leaves the tenant's plan as it is and logs an error.
    /// </summary>
    /// <remarks>
    /// Strings, not <see cref="TenantPlan"/>: the configuration binder silently
    /// drops a dictionary entry it can't convert, so a typo would unmap the
    /// price with no error. <see cref="TryParsePlan"/> checks them at startup.
    /// </remarks>
    public Dictionary<string, string> Prices { get; set; } = new(StringComparer.Ordinal);

    /// <summary>True when <paramref name="value"/> names a plan (<c>Free</c>, <c>Pro</c>, <c>Enterprise</c>, any case). Numbers are refused.</summary>
    public static bool TryParsePlan(string? value, out TenantPlan plan)
    {
        plan = default;
        var name = Enum.GetNames<TenantPlan>().FirstOrDefault(n => string.Equals(n, value, StringComparison.OrdinalIgnoreCase));
        return name is not null && Enum.TryParse(name, out plan);
    }

    /// <summary>True when <paramref name="priceId"/> is <c>price_</c> followed by something, with no whitespace.</summary>
    public static bool IsValidPriceId(string priceId) =>
        priceId.Length > PriceIdPrefix.Length
        && priceId.Length <= Tenant.StripeIdMaxLength
        && priceId.StartsWith(PriceIdPrefix, StringComparison.Ordinal)
        && !priceId.Any(char.IsWhiteSpace);

    /// <summary>True when <paramref name="secret"/> is <c>whsec_</c> followed by something.</summary>
    public static bool IsValidWebhookSecret(string secret) =>
        secret.Length > WebhookSecretPrefix.Length
        && secret.StartsWith(WebhookSecretPrefix, StringComparison.Ordinal)
        && !secret.Any(char.IsWhiteSpace);
}
