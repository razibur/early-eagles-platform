namespace EarlyEagles.Infrastructure.Identity;

/// <summary>Outcome of <see cref="SvixWebhookVerifier.Verify"/>; anything but <see cref="Valid"/> is rejected.</summary>
public enum SvixVerificationResult
{
    Valid,

    /// <summary><c>svix-id</c>, <c>svix-timestamp</c> or <c>svix-signature</c> is absent.</summary>
    MissingHeaders,

    /// <summary><c>svix-timestamp</c> is not Unix seconds.</summary>
    InvalidTimestamp,

    /// <summary>Sent more than <see cref="SvixWebhookVerifier.Tolerance"/> from now: a replay or a skewed clock.</summary>
    TimestampOutOfTolerance,

    /// <summary>No <c>v1</c> signature matches the body.</summary>
    InvalidSignature,
}
