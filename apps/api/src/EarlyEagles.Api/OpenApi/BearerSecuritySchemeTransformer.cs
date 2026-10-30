using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace EarlyEagles.Api.OpenApi;

/// <summary>
/// Declares a <c>Bearer</c> HTTP security scheme on the generated OpenAPI document
/// so Swagger UI shows an "Authorize" button and so generated clients know how to
/// attach a token. Tokens are Clerk session JWTs (per ADR-0002), validated by
/// <c>ClerkJwtBearerOptionsSetup</c> (P0-035.2); this transformer just describes the shape.
/// Operations that require authorization reference it through
/// <see cref="BearerSecurityRequirementOperationTransformer"/> (P0-027.3).
/// </summary>
public sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public const string SchemeId = "Bearer";

    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SchemeId] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Clerk-issued JWT. Paste a token from the Clerk dashboard or copy it from a signed-in /sign-in session.",
        };

        return Task.CompletedTask;
    }
}
