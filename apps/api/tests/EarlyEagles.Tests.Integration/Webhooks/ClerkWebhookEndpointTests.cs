using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using EarlyEagles.Api.Webhooks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Options;

namespace EarlyEagles.Tests.Integration.Webhooks;

// P0-036.1 / P0-037 — the Clerk webhook endpoint accepts a Svix-signed raw body
// and nothing else. Requests are signed here exactly as Svix signs them.
public sealed class ClerkWebhookEndpointTests
{
    private const string OrganizationCreated = """{"type":"organization.created","object":"event","data":{"id":"org_29w9IfBrPmcpi0IeBVaKtA7R94W","slug":"early-eagles"}}""";

    private static readonly byte[] Key = RandomNumberGenerator.GetBytes(24);
    private static readonly string Secret = "whsec_" + Convert.ToBase64String(Key);

    [Fact]
    public async Task A_Correctly_Signed_Event_Is_Accepted()
    {
        using var factory = new ClerkFactory(Secret);

        var response = await factory.CreateClient().SendAsync(Signed(OrganizationCreated));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_Body_Changed_After_Signing_Is_Rejected()
    {
        using var factory = new ClerkFactory(Secret);
        var request = Signed(OrganizationCreated);
        request.Content = Json(OrganizationCreated.Replace("early-eagles", "other-club", StringComparison.Ordinal));

        var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Invalid webhook signature", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_Unsigned_Request_Is_Rejected()
    {
        using var factory = new ClerkFactory(Secret);
        using var request = new HttpRequestMessage(HttpMethod.Post, ClerkWebhookEndpoints.Route) { Content = Json(OrganizationCreated) };

        var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_Replayed_Request_Older_Than_Five_Minutes_Is_Rejected()
    {
        using var factory = new ClerkFactory(Secret);

        var response = await factory.CreateClient().SendAsync(Signed(OrganizationCreated, DateTimeOffset.UtcNow.AddMinutes(-6)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_Signed_Body_Without_An_Event_Type_Is_Rejected()
    {
        using var factory = new ClerkFactory(Secret);

        var response = await factory.CreateClient().SendAsync(Signed("""{"data":{}}"""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Malformed webhook payload", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Signed_Body_That_Is_Not_Json_Is_Rejected()
    {
        using var factory = new ClerkFactory(Secret);

        var response = await factory.CreateClient().SendAsync(Signed("not json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_Oversized_Body_Is_Refused_Before_Verification()
    {
        using var factory = new ClerkFactory(Secret);
        var huge = "{\"type\":\"x\",\"pad\":\"" + new string('a', ClerkWebhookEndpoints.MaxBodyBytes) + "\"}";

        var response = await factory.CreateClient().SendAsync(Signed(huge));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task Without_A_Signing_Secret_The_Endpoint_Answers_503()
    {
        using var factory = new ClerkFactory(secret: null);

        var response = await factory.CreateClient().SendAsync(Signed(OrganizationCreated));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task In_Production_The_Bare_Host_Is_Not_Rejected_For_A_Missing_Tenant()
    {
        // Production returns 400 "Tenant not specified" for tenant-scoped paths on
        // the bare host; Clerk calls the bare host, so the webhook path is exempt.
        using var factory = new ClerkFactory(Secret, environment: "Production");

        var response = await factory.CreateClient().SendAsync(Signed(OrganizationCreated));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void A_Malformed_Signing_Secret_Stops_The_Host_At_Startup()
    {
        using var factory = new ClerkFactory("not-a-whsec-secret");

        var exception = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
        Assert.Contains("WebhookSigningSecret", exception.Message, StringComparison.Ordinal);
    }

    private static HttpRequestMessage Signed(string body, DateTimeOffset? sentAt = null)
    {
        var id = "msg_" + Guid.NewGuid().ToString("N");
        var timestamp = (sentAt ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var signature = Convert.ToBase64String(HMACSHA256.HashData(Key, Encoding.UTF8.GetBytes($"{id}.{timestamp}.{body}")));

        var request = new HttpRequestMessage(HttpMethod.Post, ClerkWebhookEndpoints.Route) { Content = Json(body) };
        request.Headers.Add("svix-id", id);
        request.Headers.Add("svix-timestamp", timestamp);
        request.Headers.Add("svix-signature", "v1," + signature);
        return request;
    }

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    private sealed class ClerkFactory : WebApplicationFactory<Program>
    {
        private readonly string? secret;
        private readonly string environment;

        public ClerkFactory(string? secret, string environment = "Development")
        {
            this.secret = secret;
            this.environment = environment;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("Clerk:WebhookSigningSecret", secret ?? string.Empty);
        }
    }
}
