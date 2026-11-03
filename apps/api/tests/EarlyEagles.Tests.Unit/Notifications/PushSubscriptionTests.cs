using EarlyEagles.Domain.Notifications;

namespace EarlyEagles.Tests.Unit.Notifications;

// P0-068.3 — a browser's push subscription: an https endpoint on a named host,
// two base64url keys, owned by one member; subscribing again renews it.
public class PushSubscriptionTests
{
    private const string Endpoint = "https://fcm.googleapis.com/fcm/send/dQw4w9WgXcQ:APA91bH";
    private const string P256dh = "BNcRdreALRFXTkOOUHK1EtK2wtaz5Ry4YfYCA_0QTpQtUbVlUls0VJXg7A8u-Ts1XbjhazAkj7I99e8QcYP7DkM";
    private const string Auth = "tBHItJI5svbpez7KI4CCXg";

    [Fact]
    public void Create_Records_The_Browser_And_Its_Owner()
    {
        var tenantId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;

        var subscription = PushSubscription.Create(Guid.NewGuid(), tenantId, "user_1", Endpoint, P256dh, Auth, createdAt);

        Assert.Equal(tenantId, subscription.TenantId);
        Assert.Equal("user_1", subscription.ClerkUserId);
        Assert.Equal(Endpoint, subscription.Endpoint);
        Assert.Equal(P256dh, subscription.P256dh);
        Assert.Equal(Auth, subscription.Auth);
        Assert.Equal(createdAt, subscription.CreatedAt);
    }

    [Theory]
    [InlineData("https://fcm.googleapis.com/fcm/send/abc")]
    [InlineData("https://updates.push.services.mozilla.com/wpush/v2/gAAAAA")]
    [InlineData("https://web.push.apple.com/QGuQyavXutnMH")]
    [InlineData("https://wns2-by3p.notify.windows.com/w/?token=BQYAAAB")]
    public void Real_Push_Service_Endpoints_Are_Valid(string endpoint) => Assert.True(PushSubscription.IsValidEndpoint(endpoint));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("fcm.googleapis.com/fcm/send/abc")]
    [InlineData("http://fcm.googleapis.com/fcm/send/abc")]
    [InlineData("https://localhost/push")]
    [InlineData("https://api.localhost/push")]
    [InlineData("https://127.0.0.1/push")]
    [InlineData("https://10.0.0.5/push")]
    [InlineData("https://[::1]/push")]
    [InlineData("https://intranet/push")]
    [InlineData("https://user:pass@fcm.googleapis.com/fcm/send/abc")]
    public void Endpoints_A_Push_Service_Would_Never_Use_Are_Refused(string? endpoint) =>
        Assert.False(PushSubscription.IsValidEndpoint(endpoint));

    [Fact]
    public void An_Endpoint_Longer_Than_The_Column_Is_Refused() =>
        Assert.False(PushSubscription.IsValidEndpoint("https://fcm.googleapis.com/" + new string('a', PushSubscription.EndpointMaxLength)));

    [Theory]
    [InlineData(P256dh)]
    [InlineData(Auth)]
    [InlineData("tBHItJI5svbpez7KI4CCXg==")]
    public void Base64Url_Keys_Are_Valid(string key) => Assert.True(PushSubscription.IsValidKey(key));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("==")]
    [InlineData("has space")]
    [InlineData("standard+base64/chars")]
    public void Other_Keys_Are_Refused(string? key) => Assert.False(PushSubscription.IsValidKey(key));

    [Fact]
    public void Create_Refuses_Bad_Input()
    {
        var tenantId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        Assert.Throws<ArgumentException>(() => PushSubscription.Create(Guid.Empty, tenantId, "user_1", Endpoint, P256dh, Auth, now));
        Assert.Throws<ArgumentException>(() => PushSubscription.Create(Guid.NewGuid(), Guid.Empty, "user_1", Endpoint, P256dh, Auth, now));
        Assert.Throws<ArgumentException>(() => PushSubscription.Create(Guid.NewGuid(), tenantId, " ", Endpoint, P256dh, Auth, now));
        Assert.Throws<ArgumentException>(() => PushSubscription.Create(Guid.NewGuid(), tenantId, "user_1", "https://localhost/x", P256dh, Auth, now));
        Assert.Throws<ArgumentException>(() => PushSubscription.Create(Guid.NewGuid(), tenantId, "user_1", Endpoint, "not base64url!", Auth, now));
        Assert.Throws<ArgumentException>(() => PushSubscription.Create(Guid.NewGuid(), tenantId, "user_1", Endpoint, P256dh, string.Empty, now));
    }

    [Fact]
    public void Renewing_With_The_Same_Owner_And_Keys_Changes_Nothing()
    {
        var subscription = NewSubscription();

        Assert.False(subscription.Renew("user_1", P256dh, Auth));
    }

    [Fact]
    public void Another_Member_Subscribing_On_The_Same_Browser_Takes_It_Over()
    {
        var subscription = NewSubscription();

        Assert.True(subscription.Renew("user_2", P256dh, "bmV3IGF1dGggc2VjcmV0"));

        Assert.Equal("user_2", subscription.ClerkUserId);
        Assert.Equal("bmV3IGF1dGggc2VjcmV0", subscription.Auth);
        Assert.Equal(Endpoint, subscription.Endpoint);
    }

    [Fact]
    public void Renew_Refuses_Bad_Keys_And_Keeps_The_Old_Ones()
    {
        var subscription = NewSubscription();

        Assert.Throws<ArgumentException>(() => subscription.Renew("user_2", "bad key", Auth));

        Assert.Equal("user_1", subscription.ClerkUserId);
        Assert.Equal(P256dh, subscription.P256dh);
    }

    private static PushSubscription NewSubscription() =>
        PushSubscription.Create(Guid.NewGuid(), Guid.NewGuid(), "user_1", Endpoint, P256dh, Auth, DateTimeOffset.UtcNow);
}
