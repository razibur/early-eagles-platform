using System.Net;
using System.Text;
using EarlyEagles.Api.Webhooks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Options;
using Stripe;

namespace EarlyEagles.Tests.Integration.Webhooks;

// P0-043.1 / P0-044 — the Stripe webhook endpoint accepts a Stripe-signed raw
// body and nothing else. Requests are signed with Stripe.net's own
// GenerateSignatureHeader, as `stripe trigger` would sign them.
public sealed class StripeWebhookEndpointTests
{
    private const string Secret = "whsec_integration_test_secret";

    // The Clerk endpoint shares the cap.
    private const int WebhookBodyLimit = ClerkWebhookEndpoints.MaxBodyBytes;

    private static readonly string CheckoutCompleted = Event("checkout.session.completed", StripeConfiguration.ApiVersion);

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task A_Correctly_Signed_Event_Is_Acknowledged_On_The_Bare_Host(string environment)
    {
        using var factory = new StripeFactory(Secret, environment);

        var response = await factory.CreateClient().SendAsync(Signed(CheckoutCompleted));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_Body_Changed_After_Signing_Is_Rejected()
    {
        using var factory = new StripeFactory(Secret);
        var request = Signed(CheckoutCompleted);
        request.Content = Json(CheckoutCompleted.Replace("evt_integration", "evt_forged", StringComparison.Ordinal));

        var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Invalid webhook signature", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_Unsigned_Request_Is_Rejected()
    {
        using var factory = new StripeFactory(Secret);
        using var request = new HttpRequestMessage(HttpMethod.Post, StripeWebhookEndpoints.Route) { Content = Json(CheckoutCompleted) };

        var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_Replay_Older_Than_Five_Minutes_Is_Rejected()
    {
        using var factory = new StripeFactory(Secret);

        var response = await factory.CreateClient().SendAsync(Signed(CheckoutCompleted, DateTimeOffset.UtcNow.AddMinutes(-6)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_Signed_Event_In_Another_Api_Release_Train_Is_Refused_With_The_Expected_Version()
    {
        using var factory = new StripeFactory(Secret);

        var response = await factory.CreateClient().SendAsync(Signed(Event("invoice.paid", "2020-08-27")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(StripeConfiguration.ApiVersion, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Signed_Body_That_Is_Not_An_Event_Is_Rejected()
    {
        using var factory = new StripeFactory(Secret);

        var response = await factory.CreateClient().SendAsync(Signed("""{"hello":"world"}"""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Malformed webhook payload", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_Oversized_Body_Is_Refused_Before_Verification()
    {
        using var factory = new StripeFactory(Secret);
        var huge = "{\"type\":\"x\",\"pad\":\"" + new string('a', WebhookBodyLimit) + "\"}";

        var response = await factory.CreateClient().SendAsync(Signed(huge));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task Without_A_Signing_Secret_The_Endpoint_Answers_503()
    {
        using var factory = new StripeFactory(secret: null);

        var response = await factory.CreateClient().SendAsync(Signed(CheckoutCompleted));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public void A_Malformed_Signing_Secret_Stops_The_Host_At_Startup()
    {
        using var factory = new StripeFactory("sk_test_not_a_webhook_secret");

        var exception = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
        Assert.Contains("Stripe:WebhookSigningSecret", exception.Message, StringComparison.Ordinal);
    }

    private static string Event(string type, string apiVersion) =>
        "{\"id\":\"evt_integration\",\"object\":\"event\",\"api_version\":\"" + apiVersion
        + "\",\"created\":1793361600,\"livemode\":false,\"pending_webhooks\":1,\"type\":\"" + type
        + "\",\"data\":{\"object\":{\"id\":\"cs_test_1\",\"object\":\"checkout.session\"}}}";

    private static HttpRequestMessage Signed(string payload, DateTimeOffset? at = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, StripeWebhookEndpoints.Route) { Content = Json(payload) };
        request.Headers.Add(
            StripeWebhookEndpoints.SignatureHeader,
            EventUtility.GenerateSignatureHeader(payload, Secret, (at ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds()));
        return request;
    }

    private static StringContent Json(string payload) => new(payload, Encoding.UTF8, "application/json");

    private sealed class StripeFactory : WebApplicationFactory<Program>
    {
        private readonly string? secret;
        private readonly string environment;

        public StripeFactory(string? secret, string environment = "Development")
        {
            this.secret = secret;
            this.environment = environment;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("Stripe:WebhookSigningSecret", secret ?? string.Empty);
        }
    }
}
