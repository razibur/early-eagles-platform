using Microsoft.AspNetCore.Http.Features;

namespace EarlyEagles.Api.ExceptionHandling;

/// <summary>
/// Wires the API's centralized RFC 7807 ProblemDetails exception handling.
/// Specific handlers (e.g. <see cref="ValidationExceptionHandler"/>) are
/// registered BEFORE <see cref="GlobalExceptionHandler"/> so the framework
/// gives them first refusal on each exception; the global handler is the
/// final fallback for anything not claimed.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddExceptionHandling(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
                context.ProblemDetails.Extensions["traceId"] =
                    context.HttpContext.Features.Get<IHttpActivityFeature>()?.Activity?.TraceId.ToString()
                    ?? context.HttpContext.TraceIdentifier;
            };
        });

        services.AddExceptionHandler<ValidationExceptionHandler>();
        services.AddExceptionHandler<GlobalExceptionHandler>();

        return services;
    }
}
