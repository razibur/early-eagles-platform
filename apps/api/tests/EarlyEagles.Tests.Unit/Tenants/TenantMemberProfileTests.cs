using EarlyEagles.Domain.Tenants;

namespace EarlyEagles.Tests.Unit.Tenants;

// P0-036.4.1 — a member's profile is copied from Clerk, per tenant. Clerk is
// the source of truth and the profile is cosmetic, so values that don't fit
// are dropped instead of failing the membership.
public class TenantMemberProfileTests
{
    [Fact]
    public void A_New_Member_Has_No_Profile()
    {
        var member = NewMember();

        Assert.Null(member.DisplayName);
        Assert.Null(member.Email);
        Assert.Null(member.ImageUrl);
    }

    [Fact]
    public void UpdateProfile_Trims_And_Stores_The_Values()
    {
        var member = NewMember();

        var changed = member.UpdateProfile("  Rahim Uddin ", " rahim@example.com ", " https://img.clerk.com/abc ");

        Assert.True(changed);
        Assert.Equal("Rahim Uddin", member.DisplayName);
        Assert.Equal("rahim@example.com", member.Email);
        Assert.Equal("https://img.clerk.com/abc", member.ImageUrl);
    }

    [Fact]
    public void UpdateProfile_With_The_Same_Values_Reports_No_Change()
    {
        // Webhooks arrive at least once; a redelivery is a no-op.
        var member = NewMember();
        member.UpdateProfile("Rahim Uddin", "rahim@example.com", null);

        Assert.False(member.UpdateProfile(" Rahim Uddin", "rahim@example.com", "  "));
    }

    [Fact]
    public void UpdateProfile_Replaces_The_Whole_Profile()
    {
        // Clerk sends the full user; a field Clerk cleared is cleared here too.
        var member = NewMember();
        member.UpdateProfile("Rahim Uddin", "rahim@example.com", "https://img.clerk.com/abc");

        Assert.True(member.UpdateProfile("Rahim", null, null));

        Assert.Equal("Rahim", member.DisplayName);
        Assert.Null(member.Email);
        Assert.Null(member.ImageUrl);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_Values_Become_Null(string? value)
    {
        var member = NewMember();

        member.UpdateProfile(value, value, value);

        Assert.Null(member.DisplayName);
        Assert.Null(member.Email);
        Assert.Null(member.ImageUrl);
    }

    [Theory]
    [InlineData("+15555550100")]
    [InlineData("rahim")]
    public void An_Email_Without_An_At_Sign_Is_Dropped(string identifier)
    {
        // The membership payload's identifier is a phone number for phone-OTP users.
        var member = NewMember();

        member.UpdateProfile("Rahim", identifier, null);

        Assert.Null(member.Email);
        Assert.Equal("Rahim", member.DisplayName);
    }

    [Fact]
    public void Values_Longer_Than_Their_Column_Are_Dropped_Not_Truncated()
    {
        var member = NewMember();

        member.UpdateProfile(
            new string('n', TenantMember.DisplayNameMaxLength + 1),
            new string('e', TenantMember.EmailMaxLength) + "@x",
            "https://img/" + new string('i', TenantMember.ImageUrlMaxLength));

        Assert.Null(member.DisplayName);
        Assert.Null(member.Email);
        Assert.Null(member.ImageUrl);
    }

    [Fact]
    public void Values_Exactly_At_Their_Limit_Are_Kept()
    {
        var member = NewMember();
        var name = new string('n', TenantMember.DisplayNameMaxLength);
        var email = new string('e', TenantMember.EmailMaxLength - 2) + "@x";
        var url = new string('i', TenantMember.ImageUrlMaxLength);

        member.UpdateProfile(name, email, url);

        Assert.Equal(name, member.DisplayName);
        Assert.Equal(email, member.Email);
        Assert.Equal(url, member.ImageUrl);
    }

    private static TenantMember NewMember() =>
        TenantMember.Create(Guid.NewGuid(), "user_2abc", TenantMemberRole.Member);
}
