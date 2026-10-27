using EarlyEagles.Application.Auditing;

namespace EarlyEagles.Tests.Unit.Auditing;

public class AuditEventCursorTests
{
    [Fact]
    public void Encode_Then_Decode_Round_Trips()
    {
        var original = new AuditEventCursor(
            new DateTimeOffset(2026, 10, 27, 9, 30, 15, 123, TimeSpan.Zero).AddTicks(4560),
            Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e"));

        var token = original.Encode();

        Assert.True(AuditEventCursor.TryDecode(token, out var decoded));
        Assert.Equal(original, decoded);
    }

    [Fact]
    public void Token_Is_Url_Safe()
    {
        // All-ones bytes produce '+' and '/' in standard base64.
        var token = new AuditEventCursor(DateTimeOffset.MaxValue, Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff")).Encode();

        Assert.DoesNotContain('+', token);
        Assert.DoesNotContain('/', token);
        Assert.DoesNotContain('=', token);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a cursor")]
    [InlineData("AAAA")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void TryDecode_Rejects_Malformed_Tokens(string? token)
    {
        Assert.False(AuditEventCursor.TryDecode(token, out var cursor));
        Assert.Null(cursor);
    }

    [Fact]
    public void TryDecode_Rejects_Out_Of_Range_Ticks()
    {
        // 24 bytes, but the tick field is negative.
        var bytes = new byte[24];
        Array.Fill(bytes, (byte)0xFF, 0, 8);
        var token = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        Assert.False(AuditEventCursor.TryDecode(token, out _));
    }
}
