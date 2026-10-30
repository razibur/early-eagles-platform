using EarlyEagles.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace EarlyEagles.Api.Authentication;

/// <summary>
/// Configures the JWT bearer scheme to accept Clerk session tokens (P0-035.2),
/// following Clerk's manual-verification guide: an RS256 signature from the
/// instance's keys (discovered from <see cref="ClerkOptions.Issuer"/> and
/// refreshed on rotation), <c>iss</c>, <c>exp</c> / <c>nbf</c>, and <c>azp</c>
/// against <see cref="ClerkOptions.AuthorizedParties"/>. Clerk session tokens
/// have no <c>aud</c>, so audience validation is off; <c>azp</c> is its stand-in.
/// </summary>
/// <remarks>
/// <para>Without an issuer the scheme answers "no result" for every request
/// instead of trying to validate: the API boots without Clerk, and endpoints
/// that require a user answer 401.</para>
/// <para>A pending session (<c>sts: pending</c>, e.g. a user who still has to
/// pick an organization) is refused, as Clerk's own SDKs treat it as signed out.</para>
/// <para>Claims keep their JWT names (<c>sub</c>, <c>sid</c>, <c>o</c>); nothing
/// is remapped to the long WS-Federation URIs. A <see cref="TenantRoles.ClaimType"/>
/// claim in the token itself (a custom session claim) is dropped: that role is
/// only ever granted by <see cref="TenantRoleClaimsTransformation"/>.</para>
/// </remarks>
public sealed class ClerkJwtBearerOptionsSetup : IConfigureNamedOptions<JwtBearerOptions>
{
    /// <summary>Clerk's own SDKs allow five seconds; session tokens live for 60.</summary>
    public static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(5);

    private const string PendingSessionStatus = "pending";

    private readonly IOptions<ClerkOptions> clerk;

    public ClerkJwtBearerOptionsSetup(IOptions<ClerkOptions> clerk) => this.clerk = clerk;

    public void Configure(JwtBearerOptions options) => Configure(Options.DefaultName, options);

    public void Configure(string? name, JwtBearerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (name != JwtBearerDefaults.AuthenticationScheme)
        {
            return;
        }

        var issuer = clerk.Value.Issuer;
        var authorizedParties = clerk.Value.AuthorizedParties.ToHashSet(StringComparer.Ordinal);

        options.MapInboundClaims = false;
        options.Authority = string.IsNullOrEmpty(issuer) ? null : issuer;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = issuer,
            ValidateIssuer = true,
            ValidateAudience = false,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            ClockSkew = ClockSkew,
            NameClaimType = ClerkClaims.Subject,
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (string.IsNullOrEmpty(issuer))
                {
                    context.NoResult();
                }

                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var principal = context.Principal!;

                // Clerk omits azp when the browser sent no Origin; its guide
                // says to skip the check then.
                var azp = principal.FindFirst(ClerkClaims.AuthorizedParty)?.Value;
                if (azp is not null && !authorizedParties.Contains(azp))
                {
                    context.Fail("The token's authorized party is not allowed.");
                    return Task.CompletedTask;
                }

                if (principal.FindFirst(ClerkClaims.SessionStatus)?.Value == PendingSessionStatus)
                {
                    context.Fail("The session is pending.");
                    return Task.CompletedTask;
                }

                foreach (var identity in principal.Identities)
                {
                    foreach (var claim in identity.FindAll(TenantRoles.ClaimType).ToList())
                    {
                        identity.TryRemoveClaim(claim);
                    }
                }

                return Task.CompletedTask;
            },
        };
    }
}
