using EarlyEagles.Application.Abstractions.FeatureFlags;

namespace EarlyEagles.Api.FeatureFlags;

/// <summary>
/// Puts an endpoint behind a feature flag (P1-031). While the feature is off
/// for the request's tenant the endpoint answers 404, as if it didn't exist.
/// </summary>
public static class FeatureGate
{
    public static TBuilder RequireFeature<TBuilder>(this TBuilder builder, string feature)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(feature);

        builder.AddEndpointFilter(async (context, next) =>
        {
            var flags = context.HttpContext.RequestServices.GetRequiredService<IFeatureFlags>();
            return await flags.IsEnabledAsync(feature, context.HttpContext.RequestAborted).ConfigureAwait(false)
                ? await next(context).ConfigureAwait(false)
                : Results.NotFound();
        });

        return builder;
    }
}
