using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace EarlyEagles.Api.OpenApi;

/// <summary>
/// Marks every operation that requires authorization (<c>RequireAuthorization</c>
/// / <c>[Authorize]</c>, and no <c>AllowAnonymous</c>) with the <c>Bearer</c>
/// scheme from <see cref="BearerSecuritySchemeTransformer"/> (P0-027.3), so
/// Swagger UI sends the token and generated clients know the call needs one.
/// Microsoft.AspNetCore.OpenApi does not add the requirement by itself.
/// </summary>
public sealed class BearerSecurityRequirementOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        if (!metadata.OfType<IAuthorizeData>().Any() || metadata.OfType<IAllowAnonymous>().Any())
        {
            return Task.CompletedTask;
        }

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(BearerSecuritySchemeTransformer.SchemeId, context.Document)] = [],
        });

        return Task.CompletedTask;
    }
}
