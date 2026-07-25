using EarlyEagles.Infrastructure.Tenancy;

namespace EarlyEagles.Tests.Unit.Tenancy;

// P0-031.1 — subdomain extractor happy + edge paths.
public class SubdomainExtractorTests
{
    [Theory]
    [InlineData("early-eagles.earlyeagles.club", "early-eagles")]
    [InlineData("eagles.earlyeagles.club", "eagles")]
    [InlineData("a1b2c3.earlyeagles.club", "a1b2c3")]
    public void Extracts_Slug_From_Production_Host(string host, string expected)
    {
        var ok = SubdomainExtractor.TryExtract(host, out var slug);

        Assert.True(ok);
        Assert.Equal(expected, slug);
    }

    [Theory]
    [InlineData("early-eagles.localhost", "early-eagles")]
    [InlineData("eagles.localhost", "eagles")]
    public void Extracts_Slug_From_Dev_Host(string host, string expected)
    {
        var ok = SubdomainExtractor.TryExtract(host, out var slug);

        Assert.True(ok);
        Assert.Equal(expected, slug);
    }

    [Theory]
    [InlineData("early-eagles.localhost:3000")]
    [InlineData("early-eagles.earlyeagles.club:443")]
    public void Strips_Port_Suffix_Before_Matching(string host)
    {
        var ok = SubdomainExtractor.TryExtract(host, out var slug);

        Assert.True(ok);
        Assert.Equal("early-eagles", slug);
    }

    [Fact]
    public void Lowercases_Slug_From_Uppercase_Host()
    {
        var ok = SubdomainExtractor.TryExtract("EARLY-EAGLES.EARLYEAGLES.CLUB", out var slug);

        Assert.True(ok);
        Assert.Equal("early-eagles", slug);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_Null_Or_Whitespace_Host(string? host)
    {
        var ok = SubdomainExtractor.TryExtract(host, out var slug);

        Assert.False(ok);
        Assert.Equal(string.Empty, slug);
    }

    [Theory]
    [InlineData("earlyeagles.club")]
    [InlineData("localhost")]
    [InlineData("localhost:3000")]
    public void Rejects_Bare_Base_Domain_With_No_Subdomain(string host)
    {
        var ok = SubdomainExtractor.TryExtract(host, out var slug);

        Assert.False(ok);
        Assert.Equal(string.Empty, slug);
    }

    [Theory]
    [InlineData("slug.example.com")]
    [InlineData("eagles.notearlyeagles.club")]
    [InlineData("foo.bar")]
    public void Rejects_Foreign_Base_Domain(string host)
    {
        var ok = SubdomainExtractor.TryExtract(host, out var slug);

        Assert.False(ok);
        Assert.Equal(string.Empty, slug);
    }

    [Theory]
    [InlineData("a.b.earlyeagles.club")]
    [InlineData("api.staging.earlyeagles.club")]
    [InlineData("foo.bar.localhost")]
    public void Rejects_Nested_Subdomains(string host)
    {
        var ok = SubdomainExtractor.TryExtract(host, out var slug);

        Assert.False(ok);
        Assert.Equal(string.Empty, slug);
    }

    [Theory]
    [InlineData(".earlyeagles.club")]
    [InlineData(".localhost")]
    public void Rejects_Empty_Prefix(string host)
    {
        var ok = SubdomainExtractor.TryExtract(host, out var slug);

        Assert.False(ok);
        Assert.Equal(string.Empty, slug);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("127.0.0.1:3000")]
    [InlineData("192.168.1.1")]
    public void Rejects_IPv4_Hosts(string host)
    {
        var ok = SubdomainExtractor.TryExtract(host, out var slug);

        Assert.False(ok);
    }

    [Theory]
    [InlineData("[::1]")]
    [InlineData("[::1]:3000")]
    [InlineData("[2001:db8::1]:443")]
    public void Rejects_IPv6_Hosts_Without_Crashing(string host)
    {
        var ok = SubdomainExtractor.TryExtract(host, out var slug);

        Assert.False(ok);
    }

    [Fact]
    public void Supported_Base_Domains_Are_Documented()
    {
        // Pin the list so a future contributor sees both prod and dev are
        // intentionally supported — a localhost match is not an accident.
        Assert.Contains("earlyeagles.club", SubdomainExtractor.SupportedBaseDomains);
        Assert.Contains("localhost", SubdomainExtractor.SupportedBaseDomains);
    }
}
