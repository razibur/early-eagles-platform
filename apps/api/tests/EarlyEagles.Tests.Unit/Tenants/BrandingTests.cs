using EarlyEagles.Domain.Tenants;

namespace EarlyEagles.Tests.Unit.Tenants;

public class BrandingTests
{
    [Fact]
    public void Constructor_With_Valid_Inputs_Round_Trips_Properties()
    {
        var branding = new Branding("https://cdn.example.com/logo.png", "#1A2B3C", "#FFAA00");

        Assert.Equal("https://cdn.example.com/logo.png", branding.LogoUrl);
        Assert.Equal("#1a2b3c", branding.PrimaryColor);
        Assert.Equal("#ffaa00", branding.AccentColor);
    }

    [Fact]
    public void Two_Brandings_With_Same_Components_Are_Equal()
    {
        var a = new Branding("https://cdn.example.com/logo.png", "#1a2b3c", "#ffaa00");
        var b = new Branding("https://cdn.example.com/logo.png", "#1a2b3c", "#ffaa00");

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Hex_Case_Is_Normalised_For_Equality()
    {
        var a = new Branding("https://cdn.example.com/logo.png", "#1A2B3C", "#FFAA00");
        var b = new Branding("https://cdn.example.com/logo.png", "#1a2b3c", "#ffaa00");

        Assert.Equal(a, b);
    }

    [Fact]
    public void Different_LogoUrl_Breaks_Equality()
    {
        var a = new Branding("https://cdn.example.com/a.png", "#1a2b3c", "#ffaa00");
        var b = new Branding("https://cdn.example.com/b.png", "#1a2b3c", "#ffaa00");

        Assert.NotEqual(a, b);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/relative/path.png")]
    [InlineData("ftp://cdn.example.com/logo.png")]
    [InlineData("javascript:alert(1)")]
    [InlineData("not a url")]
    public void Constructor_Rejects_Invalid_LogoUrl(string logoUrl)
    {
        var ex = Assert.Throws<ArgumentException>(
            () => new Branding(logoUrl, "#1a2b3c", "#ffaa00"));

        Assert.Equal("logoUrl", ex.ParamName);
    }

    [Fact]
    public void Constructor_Rejects_LogoUrl_Exceeding_Max_Length()
    {
        var prefix = "https://cdn.example.com/";
        var tooLong = prefix + new string('a', Branding.LogoUrlMaxLength - prefix.Length + 1);

        var ex = Assert.Throws<ArgumentException>(
            () => new Branding(tooLong, "#1a2b3c", "#ffaa00"));

        Assert.Equal("logoUrl", ex.ParamName);
    }

    [Theory]
    [InlineData("1a2b3c")] // missing #
    [InlineData("#12")] // too short
    [InlineData("#12345")] // 5 digits
    [InlineData("#1234567")] // 7 digits
    [InlineData("#123456789")] // 9 digits
    [InlineData("#xyzxyz")] // non-hex chars
    [InlineData("#")] // just hash
    [InlineData("rgb(1,2,3)")] // wrong format
    public void Constructor_Rejects_Invalid_PrimaryColor(string color)
    {
        var ex = Assert.Throws<ArgumentException>(
            () => new Branding("https://cdn.example.com/logo.png", color, "#ffaa00"));

        Assert.Equal("primaryColor", ex.ParamName);
    }

    [Fact]
    public void Constructor_Rejects_Invalid_AccentColor()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => new Branding("https://cdn.example.com/logo.png", "#1a2b3c", "not-a-color"));

        Assert.Equal("accentColor", ex.ParamName);
    }

    [Theory]
    [InlineData("#fff")]
    [InlineData("#ffffff")]
    [InlineData("#ffffffff")]
    public void Constructor_Accepts_3_6_And_8_Digit_Hex(string color)
    {
        var branding = new Branding("https://cdn.example.com/logo.png", color, "#000");

        Assert.Equal(color.ToLowerInvariant(), branding.PrimaryColor);
    }
}
