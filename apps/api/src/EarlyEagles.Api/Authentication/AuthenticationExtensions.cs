using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

namespace EarlyEagles.Api.Authentication;

public static class AuthenticationExtensions
{
    /// <summary>
    /// Clerk session tokens as the default (and only) scheme (P0-035.2), plus
    /// authorization. Configured from <c>ClerkOptions</c>, validated at startup
    /// by <c>AddInfrastructure</c>.
    /// </summary>
    public static IServiceCollection AddClerkAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ClerkJwtBearerOptionsSetup>();
        services.AddAuthorization();
        return services;
    }
}
