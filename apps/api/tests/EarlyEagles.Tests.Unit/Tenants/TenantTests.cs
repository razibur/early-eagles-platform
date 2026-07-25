using EarlyEagles.Domain.Tenants;

namespace EarlyEagles.Tests.Unit.Tenants;

// P0-028.1 — skeleton coverage only: factory happy path + slug invariant fires.
// Comprehensive invariants (slug uniqueness spec, plan transitions, branding) live in P0-028.4.
public class TenantTests
{
    [Fact]
    public void Create_With_Valid_Inputs_Returns_Tenant_With_Trimmed_DisplayName()
    {
        var id = Guid.NewGuid();

        var tenant = Tenant.Create(id, "early-eagles", "  Early Eagles Badminton Club  ");

        Assert.Equal(id, tenant.Id);
        Assert.Equal("early-eagles", tenant.Slug);
        Assert.Equal("Early Eagles Badminton Club", tenant.DisplayName);
        Assert.Null(tenant.Branding);
        Assert.Equal(TenantPlan.Free, tenant.Plan);
        Assert.Equal(TenantStatus.Active, tenant.Status);
        Assert.Empty(tenant.DomainEvents);
    }

    [Fact]
    public void SetBranding_Stores_The_Branding_Instance()
    {
        var tenant = Tenant.Create(Guid.NewGuid(), "early-eagles", "Early Eagles");
        var branding = new Branding("https://cdn.example.com/logo.png", "#1a2b3c", "#ffaa00");

        tenant.SetBranding(branding);

        Assert.Equal(branding, tenant.Branding);
    }

    [Fact]
    public void SetBranding_Replaces_Existing_Branding()
    {
        var tenant = Tenant.Create(Guid.NewGuid(), "early-eagles", "Early Eagles");
        tenant.SetBranding(new Branding("https://cdn.example.com/old.png", "#111", "#222"));

        var next = new Branding("https://cdn.example.com/new.png", "#aaa", "#bbb");
        tenant.SetBranding(next);

        Assert.Equal(next, tenant.Branding);
    }

    [Fact]
    public void SetBranding_Rejects_Null()
    {
        var tenant = Tenant.Create(Guid.NewGuid(), "early-eagles", "Early Eagles");

        Assert.Throws<ArgumentNullException>(() => tenant.SetBranding(null!));
    }

    [Fact]
    public void Create_With_Empty_Id_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => Tenant.Create(Guid.Empty, "early-eagles", "Early Eagles"));

        Assert.Equal("id", ex.ParamName);
    }

    [Theory]
    [InlineData("")] // empty
    [InlineData("a")] // below min length
    [InlineData("Early-Eagles")] // uppercase
    [InlineData("1early")] // starts with digit
    [InlineData("-early")] // starts with hyphen
    [InlineData("early-")] // ends with hyphen
    [InlineData("early--eagles")] // consecutive hyphens
    [InlineData("early_eagles")] // disallowed underscore
    [InlineData("early eagles")] // whitespace
    [InlineData("early.eagles")] // disallowed dot
    public void Create_Rejects_Invalid_Slug(string slug)
    {
        var ex = Assert.Throws<ArgumentException>(
            () => Tenant.Create(Guid.NewGuid(), slug, "Early Eagles"));

        Assert.Equal("slug", ex.ParamName);
    }

    [Fact]
    public void Create_Rejects_Slug_Exceeding_Max_Length()
    {
        var tooLong = new string('a', Tenant.SlugMaxLength + 1);

        var ex = Assert.Throws<ArgumentException>(
            () => Tenant.Create(Guid.NewGuid(), tooLong, "Early Eagles"));

        Assert.Equal("slug", ex.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Rejects_Empty_Or_Whitespace_DisplayName(string displayName)
    {
        var ex = Assert.Throws<ArgumentException>(
            () => Tenant.Create(Guid.NewGuid(), "early-eagles", displayName));

        Assert.Equal("displayName", ex.ParamName);
    }

    [Fact]
    public void Create_Rejects_DisplayName_Exceeding_Max_Length()
    {
        var tooLong = new string('a', Tenant.DisplayNameMaxLength + 1);

        var ex = Assert.Throws<ArgumentException>(
            () => Tenant.Create(Guid.NewGuid(), "early-eagles", tooLong));

        Assert.Equal("displayName", ex.ParamName);
    }
}
