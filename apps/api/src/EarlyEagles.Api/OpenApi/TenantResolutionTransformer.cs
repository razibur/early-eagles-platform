using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace EarlyEagles.Api.OpenApi;

/// <summary>
/// Documents how the API resolves the current tenant. The primary mechanism is the
/// subdomain of the request host (<c>&lt;slug&gt;.earlyeagles.club</c>), declared
/// here as a server-URL template variable. A fallback <c>X-Tenant-Slug</c> header
/// is also recognized so local-dev tools (Postman, curl, integration tests) can
/// scope requests without DNS setup — that part is documented per-operation by
/// <see cref="TenantHeaderOperationTransformer"/>.
///
/// The tenant-resolution middleware itself lands with P0-031.2; this transformer
/// makes the contract visible in Swagger UI and generated clients in advance.
/// </summary>
public sealed class TenantResolutionTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        document.Info ??= new OpenApiInfo();
        document.Info.Title ??= "EarlyEagles API";
        document.Info.Description = string.Join(
            "\n\n",
            document.Info.Description,
            "**Tenant resolution.** The API is multi-tenant. The tenant is determined per request by:",
            "1. **Subdomain (primary).** The host's leftmost label is the tenant slug — e.g. `eagles.earlyeagles.club` resolves to tenant `eagles`.",
            "2. **`X-Tenant-Slug` header (fallback).** For local development or API clients that can't set the host, set this header to the tenant slug. The middleware uses it only when the host has no usable subdomain (e.g. `localhost`).")
            .Trim();

        document.Servers =
        [
            new OpenApiServer
            {
                Url = "https://{tenant}.earlyeagles.club",
                Description = "Production — tenant resolved from the subdomain",
                Variables = new Dictionary<string, OpenApiServerVariable>
                {
                    ["tenant"] = new OpenApiServerVariable
                    {
                        Default = "app",
                        Description = "Tenant slug (the leftmost label of the host)",
                    },
                },
            },
            new OpenApiServer
            {
                Url = "https://localhost:5001",
                Description = "Local development — set X-Tenant-Slug to choose a tenant",
            },
        ];

        return Task.CompletedTask;
    }
}
