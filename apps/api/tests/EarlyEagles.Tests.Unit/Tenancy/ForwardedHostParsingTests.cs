using EarlyEagles.Api.Tenancy;
using Microsoft.Extensions.Primitives;

namespace EarlyEagles.Tests.Unit.Tenancy;

// P0-071.4 — the only X-Forwarded-Host the API will act on is the one shape the
// web app sends: a single DNS name, optionally with a port.
public class ForwardedHostParsingTests
{
    [Theory]
    [InlineData("early-eagles.earlyeagles.club")]
    [InlineData("alpha.localhost:3002")]
    [InlineData("a1b2c3.earlyeagles.club")]
    [InlineData("123club.earlyeagles.club")]
    [InlineData("earlyeagles.club:443")]
    [InlineData("localhost")]
    public void One_Dns_Name_Is_Accepted_As_Is(string value)
    {
        Assert.True(ForwardedTenantHostMiddleware.TryParseForwardedHost(new StringValues(value), out var host));
        Assert.Equal(value, host.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("alpha.earlyeagles.club, evil.earlyeagles.club")]
    [InlineData("alpha.earlyeagles.club,evil.earlyeagles.club")]
    [InlineData("alpha.earlyeagles.club ")]
    [InlineData("192.168.1.10")]
    [InlineData("[::1]:3000")]
    [InlineData("alpha.earlyeagles.club:")]
    [InlineData("alpha.earlyeagles.club:0")]
    [InlineData("alpha.earlyeagles.club:70000")]
    [InlineData("alpha.earlyeagles.club:abc")]
    [InlineData("-alpha.earlyeagles.club")]
    [InlineData("alpha-.earlyeagles.club")]
    [InlineData("alpha..earlyeagles.club")]
    [InlineData("alpha.earlyeagles.club/path")]
    [InlineData("user@alpha.earlyeagles.club")]
    [InlineData("alpha_beta.earlyeagles.club")]
    public void Anything_Else_Is_Refused(string value) =>
        Assert.False(ForwardedTenantHostMiddleware.TryParseForwardedHost(new StringValues(value), out _));

    [Fact]
    public void Two_Header_Values_Are_Refused() =>
        Assert.False(ForwardedTenantHostMiddleware.TryParseForwardedHost(new StringValues(["a.earlyeagles.club", "b.earlyeagles.club"]), out _));

    [Fact]
    public void No_Header_Is_Refused() =>
        Assert.False(ForwardedTenantHostMiddleware.TryParseForwardedHost(StringValues.Empty, out _));

    [Theory]
    [InlineData("0123456789abcdef0123456789abcdef", true)]
    [InlineData("0123456789abcdef0123456789abcde", false)]
    [InlineData("0123456789abcdef 0123456789abcdef", false)]
    public void A_Key_Needs_32_Characters_And_No_Whitespace(string key, bool valid) =>
        Assert.Equal(valid, TrustedProxyOptions.IsValidKey(key));
}
