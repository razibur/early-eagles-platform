using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EarlyEagles.Tests.Integration.OpenApi;

public class TenantResolutionDocumentationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public TenantResolutionDocumentationTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Document_Info_Description_Explains_Tenant_Resolution()
    {
        var root = await GetOpenApiRootAsync();

        var description = root.GetProperty("info").GetProperty("description").GetString();
        Assert.NotNull(description);
        Assert.Contains("Subdomain", description, StringComparison.Ordinal);
        Assert.Contains("X-Tenant-Slug", description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Document_Declares_Subdomain_Server_With_Tenant_Variable()
    {
        var root = await GetOpenApiRootAsync();

        var servers = root.GetProperty("servers");
        var hasSubdomainServer = servers.EnumerateArray()
            .Any(s => s.GetProperty("url").GetString()?.Contains("{tenant}.earlyeagles.club", StringComparison.Ordinal) ?? false);
        Assert.True(hasSubdomainServer, "subdomain server template documented");

        var subdomainServer = servers.EnumerateArray()
            .First(s => s.GetProperty("url").GetString()!.Contains("{tenant}", StringComparison.Ordinal));
        var tenantVar = subdomainServer.GetProperty("variables").GetProperty("tenant");
        Assert.False(string.IsNullOrWhiteSpace(tenantVar.GetProperty("default").GetString()));
    }

    [Fact]
    public async Task Every_Operation_Has_Optional_X_Tenant_Slug_Header_Parameter()
    {
        var root = await GetOpenApiRootAsync();
        var paths = root.GetProperty("paths");

        var operationCount = 0;
        foreach (var pathItem in paths.EnumerateObject())
        {
            foreach (var operation in pathItem.Value.EnumerateObject())
            {
                if (!operation.Value.TryGetProperty("parameters", out var parameters))
                {
                    continue;
                }

                var tenantParam = parameters.EnumerateArray()
                    .FirstOrDefault(p =>
                        p.GetProperty("in").GetString() == "header"
                        && p.GetProperty("name").GetString() == "X-Tenant-Slug");

                Assert.NotEqual(default, tenantParam);

                // OpenAPI 3.x: `required` defaults to false for non-path parameters
                // and is typically omitted in that case. Treat missing as false.
                var isRequired = tenantParam.TryGetProperty("required", out var requiredProp)
                    && requiredProp.GetBoolean();
                Assert.False(isRequired);
                operationCount++;
            }
        }

        Assert.True(operationCount > 0, "at least one operation was inspected");
    }

    private async Task<JsonElement> GetOpenApiRootAsync()
    {
        using var client = factory
            .WithWebHostBuilder(b => b.UseEnvironment("Development"))
            .CreateClient();

        var response = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }
}
