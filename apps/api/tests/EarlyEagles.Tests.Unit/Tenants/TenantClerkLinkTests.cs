using EarlyEagles.Domain.Tenants;

namespace EarlyEagles.Tests.Unit.Tenants;

// P0-036.2.1 — a tenant is linked to exactly one Clerk organization, can be
// renamed from Clerk, and exposes the slug / name rules for webhook pre-checks.
public class TenantClerkLinkTests
{
    private const string OrgId = "org_29w9IfBrPmcpi0IeBVaKtA7R94W";

    [Fact]
    public void A_New_Tenant_Has_No_Clerk_Organization()
    {
        Assert.Null(Tenant.Create(Guid.NewGuid(), "early-eagles", "Early Eagles").ClerkOrganizationId);
    }

    [Fact]
    public void LinkClerkOrganization_Sets_The_Id()
    {
        var tenant = Tenant.Create(Guid.NewGuid(), "early-eagles", "Early Eagles");

        tenant.LinkClerkOrganization(OrgId);

        Assert.Equal(OrgId, tenant.ClerkOrganizationId);
    }

    [Fact]
    public void Linking_The_Same_Organization_Again_Is_A_No_Op()
    {
        // Webhooks arrive at least once; a redelivery must not fail.
        var tenant = Tenant.Create(Guid.NewGuid(), "early-eagles", "Early Eagles");
        tenant.LinkClerkOrganization(OrgId);

        tenant.LinkClerkOrganization(OrgId);

        Assert.Equal(OrgId, tenant.ClerkOrganizationId);
    }

    [Fact]
    public void Linking_A_Different_Organization_Throws()
    {
        var tenant = Tenant.Create(Guid.NewGuid(), "early-eagles", "Early Eagles");
        tenant.LinkClerkOrganization(OrgId);

        Assert.Throws<InvalidOperationException>(() => tenant.LinkClerkOrganization("org_someoneelse"));
        Assert.Equal(OrgId, tenant.ClerkOrganizationId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void LinkClerkOrganization_Rejects_Blank_Ids(string id)
    {
        var tenant = Tenant.Create(Guid.NewGuid(), "early-eagles", "Early Eagles");

        Assert.Throws<ArgumentException>(() => tenant.LinkClerkOrganization(id));
    }

    [Fact]
    public void LinkClerkOrganization_Rejects_An_Id_Longer_Than_The_Column()
    {
        var tenant = Tenant.Create(Guid.NewGuid(), "early-eagles", "Early Eagles");

        Assert.Throws<ArgumentException>(
            () => tenant.LinkClerkOrganization("org_" + new string('x', Tenant.ClerkOrganizationIdMaxLength)));
    }

    [Fact]
    public void Rename_Trims_And_Stores_The_Name()
    {
        var tenant = Tenant.Create(Guid.NewGuid(), "early-eagles", "Early Eagles");

        tenant.Rename("  Early Eagles Badminton  ");

        Assert.Equal("Early Eagles Badminton", tenant.DisplayName);
    }

    [Fact]
    public void Rename_Rejects_A_Blank_Name_And_Keeps_The_Old_One()
    {
        var tenant = Tenant.Create(Guid.NewGuid(), "early-eagles", "Early Eagles");

        Assert.Throws<ArgumentException>(() => tenant.Rename("  "));
        Assert.Equal("Early Eagles", tenant.DisplayName);
    }

    [Fact]
    public void A_Deactivated_Tenant_Cannot_Be_Renamed()
    {
        var tenant = Tenant.Create(Guid.NewGuid(), "early-eagles", "Early Eagles");
        tenant.Deactivate();

        Assert.Throws<InvalidOperationException>(() => tenant.Rename("New Name"));
    }

    [Theory]
    [InlineData("early-eagles", true)]
    [InlineData("ab", true)]
    [InlineData("club42", true)]
    [InlineData("Early-Eagles", false)]
    [InlineData("early_eagles", false)]
    [InlineData("early--eagles", false)]
    [InlineData("-early", false)]
    [InlineData("a", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidSlug_Agrees_With_Create(string? slug, bool expected)
    {
        Assert.Equal(expected, Tenant.IsValidSlug(slug));
        if (slug is not null)
        {
            var created = Record.Exception(() => Tenant.Create(Guid.NewGuid(), slug, "Club"));
            Assert.Equal(expected, created is null);
        }
    }

    [Fact]
    public void IsValidSlug_Rejects_A_Slug_Over_The_Max_Length()
    {
        Assert.False(Tenant.IsValidSlug(new string('a', Tenant.SlugMaxLength + 1)));
    }

    [Theory]
    [InlineData("Early Eagles", true)]
    [InlineData("  padded  ", true)]
    [InlineData("   ", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidDisplayName_Matches_The_Trimmed_Rules(string? name, bool expected)
    {
        Assert.Equal(expected, Tenant.IsValidDisplayName(name));
    }
}
