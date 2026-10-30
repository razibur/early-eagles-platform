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
// an allowed (or absent) azp and an active session. Tokens are signed here with
// a local RSA key that stands in for the instance's JWKS, so no Clerk account
// or network is involved; everything after key discovery is the real pipeline.
public sealed class ClerkJwtAuthenticationTests
{
    private const string Issuer = "https://test-instance.clerk.accounts.dev";
    private const string AllowedOrigin = "https://earlyeagles.club";
    private const string UserId = "user_2abcJwtCheck";
    private const string SessionId = "sess_2abcJwtCheck";

    private static readonly RsaSecurityKey InstanceKey = NewKey("ins_key_1");

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
        Assert.Equal(new MeResponse(UserId, SessionId), body);
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

    [Theory]
    [InlineData("http://test-instance.clerk.accounts.dev", AllowedOrigin)]
    [InlineData("https://test-instance.clerk.accounts.dev/", AllowedOrigin)]
    [InlineData("not a url", AllowedOrigin)]
    [InlineData(Issuer, "https://earlyeagles.club/app")]
    [InlineData(Issuer, "earlyeagles.club")]
    public void Malformed_Clerk_Settings_Stop_The_Host_At_Startup(string issuer, string authorizedParty)
    {
        using var factory = new ClerkJwtFactory(issuer, authorizedParty);

        var error = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("Clerk:", error.ToString(), StringComparison.Ordinal);
    }

    private static Task<HttpResponseMessage> SendAsync(WebApplicationFactory<Program> factory, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, MeEndpoints.Route);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return factory.CreateClient().SendAsync(request);
    }

    private static string Token(
        string issuer = Issuer,
        string? azp = AllowedOrigin,
        RsaSecurityKey? key = null,
        SigningCredentials? credentials = null,
        DateTime? notBefore = null,
        DateTime? expires = null,
        Dictionary<string, object>? extra = null)
    {
        var now = DateTime.UtcNow;
        var claims = new Dictionary<string, object>
        {
            ["sub"] = UserId,
            ["sid"] = SessionId,
            ["v"] = 2,
        };
        if (azp is not null)
        {
            claims["azp"] = azp;
        }

        foreach (var (name, value) in extra ?? [])
        {
            claims[name] = value;
        }

        var start = notBefore ?? now.AddSeconds(-10);
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            IssuedAt = start,
            NotBefore = start,
            Expires = expires ?? now.AddSeconds(60),
            Claims = claims,
            SigningCredentials = credentials ?? new SigningCredentials(key ?? InstanceKey, SecurityAlgorithms.RsaSha256),
        });
    }

    private static RsaSecurityKey NewKey(string keyId) => new(RSA.Create(2048)) { KeyId = keyId };

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
            builder.UseSetting("Clerk:Issuer", issuer ?? string.Empty);
            builder.UseSetting("Clerk:AuthorizedParties:0", authorizedParty);
            builder.ConfigureServices(services =>
            {
                // Stand-in for the instance's JWKS: the signing key is served
                // from memory instead of {issuer}/.well-known/openid-configuration.
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    var configuration = new OpenIdConnectConfiguration { Issuer = issuer };
                    configuration.SigningKeys.Add(InstanceKey);
                    options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
                });
            });
        }
    }
}
