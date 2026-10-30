namespace EarlyEagles.Infrastructure.Billing;

/// <summary>Why <see cref="StripeWebhookVerifier"/> accepted or refused a delivery.</summary>
public enum StripeWebhookVerificationResult
{
    /// <summary>Signed with the secret, within five minutes, and an event this SDK reads.</summary>
    Valid,

    /// <summary>No <c>Stripe-Signature</c> header.</summary>
    MissingSignature,

    /// <summary>
    /// The header is malformed, no <c>v1</c> signature matches the body, or
    /// its timestamp is more than five minutes from now (a replay).
    /// </summary>
    InvalidSignature,

    /// <summary>Correctly signed, but not a Stripe event object.</summary>
    Malformed,

    /// <summary>
    /// Correctly signed, but sent with an API version from another release
    /// train than Stripe.net's. A configuration problem in Stripe, not an
    /// attack: the endpoint's API version must be changed to match.
    /// </summary>
    ApiVersionMismatch,
}
