using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EarlyEagles.Tests.Integration.OpenApi;

/// <summary>
/// /openapi/v1.json and /swagger are gated on non-production environments.
/// WebApplicationFactory&lt;Program&gt; defaults to Production, so each test
/// overrides the environment to Development.
/// </summary>
public class SwaggerEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public SwaggerEndpointTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task OpenApi_Document_Is_Reachable_And_Lists_Health_Endpoint()
    {
        using var client = factory
            .WithWebHostBuilder(b => b.UseEnvironment("Development"))
            .CreateClient();

        var response = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("openapi", out var version), "openapi field present");
        Assert.StartsWith("3.", version.GetString(), StringComparison.Ordinal);

        Assert.True(root.TryGetProperty("paths", out var paths), "paths object present");
        Assert.True(paths.TryGetProperty("/health", out _), "/health path documented");
    }

    [Fact]
    public async Task Swagger_Ui_Returns_Html_In_Non_Production()
    {
        using var client = factory
            .WithWebHostBuilder(b => b.UseEnvironment("Development"))
            .CreateClient();

        var response = await client.GetAsync(new Uri("/swagger/index.html", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task OpenApi_Document_Declares_Bearer_Security_Scheme()
    {
        using var client = factory
            .WithWebHostBuilder(b => b.UseEnvironment("Development"))
            .CreateClient();

        var response = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("components", out var components), "components present");
        Assert.True(
            components.TryGetProperty("securitySchemes", out var schemes),
            "components.securitySchemes present");
        Assert.True(schemes.TryGetProperty("Bearer", out var bearer), "Bearer scheme present");

        Assert.Equal("http", bearer.GetProperty("type").GetString());
        Assert.Equal("bearer", bearer.GetProperty("scheme").GetString());
        Assert.Equal("JWT", bearer.GetProperty("bearerFormat").GetString());
        Assert.False(string.IsNullOrWhiteSpace(bearer.GetProperty("description").GetString()));
    }

    [Theory]
    [InlineData("/me", true)]
    [InlineData("/audit-events", true)]
    [InlineData("/health", false)]
    [InlineData("/whoami", false)]
    public async Task Only_Operations_That_Require_Authorization_Reference_The_Bearer_Scheme(string path, bool secured)
    {
        // P0-027.3 — Microsoft.AspNetCore.OpenApi doesn't add the requirement itself.
        using var client = factory
            .WithWebHostBuilder(b => b.UseEnvironment("Development"))
            .CreateClient();

        using var document = JsonDocument.Parse(await client.GetStringAsync(new Uri("/openapi/v1.json", UriKind.Relative)));
        var operation = document.RootElement.GetProperty("paths").GetProperty(path).GetProperty("get");

        if (secured)
        {
            var requirement = Assert.Single(operation.GetProperty("security").EnumerateArray());
            Assert.True(requirement.TryGetProperty("Bearer", out var scopes), "requirement names the Bearer scheme");
            Assert.Equal(0, scopes.GetArrayLength());
        }
        else
        {
            Assert.False(operation.TryGetProperty("security", out _), $"{path} must not require a token");
        }
    }

    [Fact]
    public async Task Swagger_Ui_Is_Not_Mapped_In_Production()
    {
        using var client = factory
            .WithWebHostBuilder(b => b.UseEnvironment("Production"))
            .CreateClient();

        var response = await client.GetAsync(new Uri("/swagger/index.html", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
