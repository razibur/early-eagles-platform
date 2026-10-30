using System.Text.Json;
using Stripe;

namespace EarlyEagles.Infrastructure.Billing;

/// <summary>
/// Checks a Stripe webhook delivery (P0-044) and reads its event: the
/// <c>Stripe-Signature</c> header must carry an HMAC-SHA256 of
/// <c>"{t}.{body}"</c> under the endpoint's <c>whsec_</c> secret, with
/// <c>t</c> no more than five minutes from now.
/// </summary>
/// <remarks>
/// <para>The check itself is Stripe.net's <see cref="EventUtility.ValidateSignature(string, string, string, long, long)"/>,
/// fed the time from <see cref="TimeProvider"/> so tests can move the clock.
/// The signature is checked first, and only a correctly signed body is parsed.</para>
/// <para>Parsing refuses an event from another API release train than the
/// SDK's (<see cref="StripeConfiguration.ApiVersion"/>): its objects may not
/// have the shape the SDK expects. That case gets its own result, because the
/// fix is in the Stripe dashboard, not here.</para>
/// </remarks>
public sealed class StripeWebhookVerifier
{
    private readonly string secret;
    private readonly TimeProvider timeProvider;

    public StripeWebhookVerifier(string secret, TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrEmpty(secret);
        ArgumentNullException.ThrowIfNull(timeProvider);
        this.secret = secret;
        this.timeProvider = timeProvider;
    }

    /// <summary>
    /// Verifies <paramref name="payload"/> (the body exactly as received)
    /// against <paramref name="signatureHeader"/>. <paramref name="stripeEvent"/>
    /// is set only for <see cref="StripeWebhookVerificationResult.Valid"/>.
    /// </summary>
    public StripeWebhookVerificationResult Verify(string payload, string? signatureHeader, out Event? stripeEvent)
    {
        ArgumentNullException.ThrowIfNull(payload);
        stripeEvent = null;
        if (string.IsNullOrWhiteSpace(signatureHeader))
        {
            return StripeWebhookVerificationResult.MissingSignature;
        }

        try
        {
            EventUtility.ValidateSignature(
                payload,
                signatureHeader,
                secret,
                EventUtility.DefaultTimeTolerance,
                timeProvider.GetUtcNow().ToUnixTimeSeconds());
        }
        catch (StripeException)
        {
            return StripeWebhookVerificationResult.InvalidSignature;
        }

        if (!IsEventEnvelope(payload))
        {
            return StripeWebhookVerificationResult.Malformed;
        }

        try
        {
            stripeEvent = EventUtility.ParseEvent(payload, throwOnApiVersionMismatch: true);
        }
        catch (StripeException)
        {
            return StripeWebhookVerificationResult.ApiVersionMismatch;
        }
        catch (Exception ex) when (ex is ArgumentException or JsonException)
        {
            return StripeWebhookVerificationResult.Malformed;
        }

        return StripeWebhookVerificationResult.Valid;
    }

    // Stripe.net assumes the envelope: a body without api_version throws a
    // NullReferenceException from inside ParseEvent. Check it first.
    private static bool IsEventEnvelope(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object
                && HasString(root, "id")
                && HasString(root, "type")
                && HasString(root, "api_version");
        }
        catch (JsonException)
        {
            return false;
        }

        static bool HasString(JsonElement root, string name) =>
            root.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.String
            && !string.IsNullOrEmpty(value.GetString());
    }
}
