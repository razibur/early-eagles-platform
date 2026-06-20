using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace EarlyEagles.Api.OpenApi;

/// <summary>
/// Adds an optional <c>X-Tenant-Slug</c> header parameter to every operation so
/// clients (and Swagger UI's "Try it out") can scope requests in environments
/// without subdomain DNS. See <see cref="TenantResolutionTransformer"/> for the
/// document-level description of the dual-resolution mechanism.
///
/// Diagnostic endpoints (<c>/health</c>, <c>/ready</c>) are tenant-agnostic but
/// still get the parameter — it's optional, so they ignore it. When tenant-
/// scoped endpoints land in Phase 1 the parameter actually starts mattering;
/// no per-endpoint exclusion list is worth maintaining yet.
/// </summary>
public sealed class TenantHeaderOperationTransformer : IOpenApiOperationTransformer
{
    private const string HeaderName = "X-Tenant-Slug";

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        operation.Parameters ??= [];

        if (operation.Parameters.Any(p => string.Equals(p.Name, HeaderName, StringComparison.OrdinalIgnoreCase)))
        {
            return Task.CompletedTask;
        }

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = HeaderName,
            In = ParameterLocation.Header,
            Required = false,
            Description = "Tenant slug fallback for clients that can't set a subdomain (local dev, Postman). Ignored when the host's leftmost label is a known tenant.",
            Schema = new OpenApiSchema { Type = JsonSchemaType.String },
        });

        return Task.CompletedTask;
    }
}
