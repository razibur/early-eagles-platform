namespace EarlyEagles.Api.Webhooks;

/// <summary>
/// Reads a webhook body exactly as received, for signature checks over the
/// raw bytes (Clerk P0-037, Stripe P0-044).
/// </summary>
internal static class WebhookBody
{
    /// <summary>Clerk and Stripe events are a few KB; anything this size is not one.</summary>
    public const int MaxBytes = 1024 * 1024;

    /// <summary>The body as received, or null when it exceeds <see cref="MaxBytes"/>.</summary>
    public static async Task<byte[]?> ReadAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.ContentLength > MaxBytes)
        {
            return null;
        }

        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await request.Body.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > MaxBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}
