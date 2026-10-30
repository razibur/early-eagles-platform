using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

namespace EarlyEagles.Api.Authentication;

public static class AuthenticationExtensions
{
    /// <summary>
    /// Clerk session tokens as the default (and only) scheme (P0-035.2), plus
    /// authorization with the tenant policies (P0-038.2). Configured from <c>ClerkOptions</c>, validated at startup
    /// by <c>AddInfrastructure</c>. The tenant role claim (P0-038.1) is added
    /// per request, so the transformation is scoped with the tenant context.
    /// </summary>
    public static IServiceCollection AddClerkAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ClerkJwtBearerOptionsSetup>();
        services.AddScoped<IClaimsTransformation, TenantRoleClaimsTransformation>();
        services.AddAuthorizationBuilder().AddTenantPolicies();
        return services;
    }
}
