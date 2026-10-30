using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace EarlyEagles.Tests.Integration.Authentication;

/// <summary>
/// Clerk session tokens for tests, signed with a local RSA key that stands in
/// for the instance's JWKS. No Clerk account or network is involved; everything
/// after key discovery is the real pipeline.
/// </summary>
internal static class ClerkTestTokens
{
    public const string Issuer = "https://test-instance.clerk.accounts.dev";
    public const string AllowedOrigin = "https://earlyeagles.club";

    public static readonly RsaSecurityKey InstanceKey = NewKey("ins_key_1");

    public static RsaSecurityKey NewKey(string keyId) => new(RSA.Create(2048)) { KeyId = keyId };

    /// <summary>A v2 session token; <paramref name="extra"/> adds or overrides claims.</summary>
    public static string Create(
        string userId,
        string? sessionId,
        string issuer = Issuer,
        string? azp = AllowedOrigin,
        RsaSecurityKey? key = null,
        SigningCredentials? credentials = null,
        DateTime? notBefore = null,
        DateTime? expires = null,
        Dictionary<string, object>? extra = null)
    {
        var now = DateTime.UtcNow;
        var claims = new Dictionary<string, object> { ["sub"] = userId, ["v"] = 2 };
        if (sessionId is not null)
        {
            claims["sid"] = sessionId;
        }

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

    /// <summary>A v2 <c>o</c> claim: the active organization and the role in it, unprefixed as Clerk sends it.</summary>
    public static Dictionary<string, object> ActiveOrganization(string organizationId, string role) =>
        new() { ["o"] = new Dictionary<string, object> { ["id"] = organizationId, ["rol"] = role } };

    /// <summary>Points the host at <paramref name="issuer"/> and serves <see cref="InstanceKey"/> as its signing key.</summary>
    public static void UseTestIssuer(IWebHostBuilder builder, string? issuer = Issuer, string authorizedParty = AllowedOrigin)
    {
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

    public static HttpRequestMessage Get(string route, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, route);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}
