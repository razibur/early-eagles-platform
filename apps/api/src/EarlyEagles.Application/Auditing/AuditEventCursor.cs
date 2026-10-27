using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;

namespace EarlyEagles.Application.Auditing;

/// <summary>
/// Position in the audit log for keyset paging (P0-048): the last row a page
/// returned. The next page is everything strictly older, ordered by
/// <c>(OccurredAt DESC, Id DESC)</c>; the id breaks ties between events
/// recorded in the same microsecond.
/// </summary>
/// <remarks>
/// Serialized as an opaque base64url token (UTC ticks + id bytes) so clients
/// treat it as a bookmark, not something to construct. It carries no tenant:
/// the query filter scopes every page, so a token from another tenant just
/// pages through the caller's own log.
/// </remarks>
public readonly record struct AuditEventCursor(DateTimeOffset OccurredAt, Guid Id)
{
    private const int TicksLength = sizeof(long);
    private const int GuidLength = 16;
    private const int TokenLength = TicksLength + GuidLength;

    public string Encode()
    {
        Span<byte> bytes = stackalloc byte[TokenLength];
        BinaryPrimitives.WriteInt64BigEndian(bytes, OccurredAt.UtcTicks);
        Id.TryWriteBytes(bytes[TicksLength..]);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static bool TryDecode(string? token, [NotNullWhen(true)] out AuditEventCursor? cursor)
    {
        cursor = null;
        if (string.IsNullOrEmpty(token) || token.Length > 64)
        {
            return false;
        }

        var base64 = token.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + ((4 - (base64.Length % 4)) % 4), '=');

        Span<byte> bytes = stackalloc byte[TokenLength + 3];
        if (!Convert.TryFromBase64String(base64, bytes, out var written) || written != TokenLength)
        {
            return false;
        }

        var ticks = BinaryPrimitives.ReadInt64BigEndian(bytes);
        if (ticks < DateTimeOffset.MinValue.UtcTicks || ticks > DateTimeOffset.MaxValue.UtcTicks)
        {
            return false;
        }

        cursor = new AuditEventCursor(new DateTimeOffset(ticks, TimeSpan.Zero), new Guid(bytes.Slice(TicksLength, GuidLength)));
        return true;
    }
}
