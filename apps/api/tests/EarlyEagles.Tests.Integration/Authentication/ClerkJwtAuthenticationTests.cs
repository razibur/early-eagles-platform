using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using EarlyEagles.Api.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace EarlyEagles.Tests.Integration.Authentication;

// P0-035.2 — Clerk session tokens are accepted only with a valid RS256
// signature from the instance's keys, the configured issuer, a live lifetime,
// an allowed (or absent) azp and an active session. P0-035.3 — an accepted
// token's user, session and active organization reach the request. Tokens are signed here with
// a local RSA key that stands in for the instance's JWKS, so no Clerk account
// or network is involved; everything after key discovery is the real pipeline.
public sealed class ClerkJwtAuthenticationTests
{
    private const string Issuer = ClerkTestTokens.Issuer;
    private const string AllowedOrigin = ClerkTestTokens.AllowedOrigin;
    private const string UserId = "user_2abcJwtCheck";
    private const string SessionId = "sess_2abcJwtCheck";

    [Fact]
    public async Task A_Request_Without_A_Token_Is_Unauthorized()
    {
        using var factory = new ClerkJwtFactory(Issuer);

        var response = await factory.CreateClient().GetAsync(new Uri(MeEndpoints.Route, UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_Valid_Token_Is_Accepted_With_Its_User_And_Session()
    {
        using var factory = new ClerkJwtFactory(Issuer);

        var response = await SendAsync(factory, Token());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<MeResponse>();
        Assert.Equal(new MeResponse(UserId, SessionId, Organization: null, TenantRole: null), body);
    }

    [Fact]
    public async Task The_V2_Organization_Claim_Gives_The_Active_Organization_With_A_Prefixed_Role()
    {
        using var factory = new ClerkJwtFactory(Issuer);
        var organization = new Dictionary<string, object> { ["id"] = "org_2abcClub", ["slg"] = "early-eagles", ["rol"] = "admin", ["per"] = string.Empty, ["fpm"] = string.Empty };

        var body = await MeAsync(factory, Token(extra: new Dictionary<string, object> { ["o"] = organization }));

        Assert.Equal(new ClerkActiveOrganization("org_2abcClub", "early-eagles", "org:admin"), body.Organization);
    }

    [Fact]
    public async Task The_V1_Organization_Claims_Give_The_Active_Organization()
    {
        using var factory = new ClerkJwtFactory(Issuer);
        var claims = new Dictionary<string, object> { ["org_id"] = "org_2abcClub", ["org_slug"] = "early-eagles", ["org_role"] = "org:member" };

        var body = await MeAsync(factory, Token(extra: claims));

        Assert.Equal(new ClerkActiveOrganization("org_2abcClub", "early-eagles", "org:member"), body.Organization);
    }

    [Theory]
    [InlineData("not-an-object")]
    [InlineData("{}")]
    public async Task An_Unreadable_Organization_Claim_Means_No_Active_Organization(string value)
    {
        // A JSON string; the second is an object written as a string, which
        // Clerk never sends. Neither may produce an organization.
        using var factory = new ClerkJwtFactory(Issuer);

        var body = await MeAsync(factory, Token(extra: new Dictionary<string, object> { ["o"] = value }));

        Assert.Null(body.Organization);
    }

    [Fact]
    public async Task An_Organization_Without_An_Id_Means_No_Active_Organization()
    {
        using var factory = new ClerkJwtFactory(Issuer);
        var organization = new Dictionary<string, object> { ["slg"] = "early-eagles", ["rol"] = "admin" };

        var body = await MeAsync(factory, Token(extra: new Dictionary<string, object> { ["o"] = organization }));

        Assert.Null(body.Organization);
    }

    [Fact]
    public async Task A_Token_Without_An_Authorized_Party_Is_Accepted()
    {
        // Clerk omits azp when the browser sent no Origin header.
        using var factory = new ClerkJwtFactory(Issuer);

        var response = await SendAsync(factory, Token(azp: null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_Token_Signed_With_Another_Key_Is_Unauthorized()
    {
        using var factory = new ClerkJwtFactory(Issuer);

        var response = await SendAsync(factory, Token(key: NewKey("ins_key_1")));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_Token_Signed_With_A_Symmetric_Key_Is_Unauthorized()
    {
        using var factory = new ClerkJwtFactory(Issuer);
        var hmac = new SigningCredentials(new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32)), SecurityAlgorithms.HmacSha256);

        var response = await SendAsync(factory, Token(credentials: hmac));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task An_Expired_Token_Is_Unauthorized()
    {
        using var factory = new ClerkJwtFactory(Issuer);
        var now = DateTime.UtcNow;

        var response = await SendAsync(factory, Token(notBefore: now.AddMinutes(-5), expires: now.AddSeconds(-30)));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_Token_Not_Yet_Valid_Is_Unauthorized()
    {
        using var factory = new ClerkJwtFactory(Issuer);
        var now = DateTime.UtcNow;

        var response = await SendAsync(factory, Token(notBefore: now.AddSeconds(30), expires: now.AddMinutes(1)));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_Token_From_Another_Issuer_Is_Unauthorized()
    {
        using var factory = new ClerkJwtFactory(Issuer);

        var response = await SendAsync(factory, Token(issuer: "https://someone-else.clerk.accounts.dev"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_Token_For_Another_Origin_Is_Unauthorized()
    {
        using var factory = new ClerkJwtFactory(Issuer);

        var response = await SendAsync(factory, Token(azp: "https://evil.example"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_Pending_Session_Is_Unauthorized()
    {
        using var factory = new ClerkJwtFactory(Issuer);

        var response = await SendAsync(factory, Token(extra: new Dictionary<string, object> { ["sts"] = "pending" }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Without_An_Issuer_No_Token_Is_Accepted_And_Public_Endpoints_Still_Answer()
    {
        using var factory = new ClerkJwtFactory(issuer: null);
        var client = factory.CreateClient();

        var me = await SendAsync(factory, Token());
        var health = await client.GetAsync(new Uri("/health", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    private static Task<HttpResponseMessage> SendAsync(WebApplicationFactory<Program> factory, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, MeEndpoints.Route);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return factory.CreateClient().SendAsync(request);
    }

    private static async Task<MeResponse> MeAsync(WebApplicationFactory<Program> factory, string token)
    {
        var response = await SendAsync(factory, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MeResponse>())!;
    }

    private static string Token(
        string issuer = Issuer,
        string? azp = AllowedOrigin,
        RsaSecurityKey? key = null,
        SigningCredentials? credentials = null,
        DateTime? notBefore = null,
        DateTime? expires = null,
        Dictionary<string, object>? extra = null) =>
        ClerkTestTokens.Create(UserId, SessionId, issuer, azp, key, credentials, notBefore, expires, extra);

    private static RsaSecurityKey NewKey(string keyId) => ClerkTestTokens.NewKey(keyId);

    private sealed class ClerkJwtFactory : WebApplicationFactory<Program>
    {
        private readonly string? issuer;
        private readonly string authorizedParty;

        public ClerkJwtFactory(string? issuer, string authorizedParty = AllowedOrigin)
        {
            this.issuer = issuer;
            this.authorizedParty = authorizedParty;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            ClerkTestTokens.UseTestIssuer(builder, issuer, authorizedParty);
        }
    }
}
