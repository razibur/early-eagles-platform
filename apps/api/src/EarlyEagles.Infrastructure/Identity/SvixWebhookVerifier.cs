using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace EarlyEagles.Infrastructure.Identity;

/// <summary>
/// Verifies a webhook signed the Svix way, which is how Clerk signs its
/// webhooks (P0-037): HMAC-SHA256 over <c>{svix-id}.{svix-timestamp}.{raw body}</c>
/// with the base64 key after the <c>whsec_</c> prefix, sent as one or more
/// space-separated <c>v1,&lt;base64&gt;</c> entries in <c>svix-signature</c>
/// (several during a secret rotation).
/// </summary>
/// <remarks>
/// Hand-written instead of the Svix NuGet package, which is a full API client;
/// this is the only part we need. The timestamp check (±5 minutes) stops a
/// captured request being replayed later. Signatures are compared in constant
/// time. The raw bytes are verified, so the body must not be parsed or
/// re-serialised before this runs.
/// </remarks>
public sealed class SvixWebhookVerifier
{
    public const string SecretPrefix = "whsec_";

    public static readonly TimeSpan Tolerance = TimeSpan.FromMinutes(5);

    private const string SignatureVersion = "v1";

    private readonly byte[] key;
    private readonly TimeProvider timeProvider;

    public SvixWebhookVerifier(string signingSecret, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (!TryDecodeSecret(signingSecret, out var decoded))
        {
            throw new ArgumentException($"Expected a '{SecretPrefix}<base64>' signing secret.", nameof(signingSecret));
        }

        key = decoded;
        this.timeProvider = timeProvider;
    }

    /// <summary>True when <paramref name="signingSecret"/> has the <c>whsec_&lt;base64&gt;</c> shape.</summary>
    public static bool IsValidSecret(string? signingSecret) => TryDecodeSecret(signingSecret, out _);

    public SvixVerificationResult Verify(string? messageId, string? timestamp, string? signatureHeader, ReadOnlySpan<byte> body)
    {
        if (string.IsNullOrEmpty(messageId) || string.IsNullOrEmpty(timestamp) || string.IsNullOrEmpty(signatureHeader))
        {
            return SvixVerificationResult.MissingHeaders;
        }

        if (!long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
        {
            return SvixVerificationResult.InvalidTimestamp;
        }

        var sent = DateTimeOffset.FromUnixTimeSeconds(seconds);
        var now = timeProvider.GetUtcNow();
        if (sent < now - Tolerance || sent > now + Tolerance)
        {
            return SvixVerificationResult.TimestampOutOfTolerance;
        }

        var expected = ComputeSignature(messageId, timestamp, body);
        foreach (var entry in signatureHeader.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var comma = entry.IndexOf(',', StringComparison.Ordinal);
            if (comma <= 0 || !entry.AsSpan(0, comma).SequenceEqual(SignatureVersion))
            {
                continue;
            }

            var candidate = new byte[expected.Length];
            if (Convert.TryFromBase64String(entry[(comma + 1)..], candidate, out var written)
                && written == expected.Length
                && CryptographicOperations.FixedTimeEquals(candidate, expected))
            {
                return SvixVerificationResult.Valid;
            }
        }

        return SvixVerificationResult.InvalidSignature;
    }

    private static bool TryDecodeSecret(string? signingSecret, out byte[] decoded)
    {
        decoded = [];
        if (string.IsNullOrEmpty(signingSecret) || !signingSecret.StartsWith(SecretPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var encoded = signingSecret[SecretPrefix.Length..];
        var buffer = new byte[encoded.Length];
        if (!Convert.TryFromBase64String(encoded, buffer, out var written) || written == 0)
        {
            return false;
        }

        decoded = buffer[..written];
        return true;
    }

    private byte[] ComputeSignature(string messageId, string timestamp, ReadOnlySpan<byte> body)
    {
        var prefix = Encoding.UTF8.GetBytes($"{messageId}.{timestamp}.");
        var signedContent = new byte[prefix.Length + body.Length];
        prefix.CopyTo(signedContent, 0);
        body.CopyTo(signedContent.AsSpan(prefix.Length));
        return HMACSHA256.HashData(key, signedContent);
    }
}
