using System.Text.Json;
using EarlyEagles.Api.ExceptionHandling;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace EarlyEagles.Tests.Integration.ExceptionHandling;

public class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task Unhandled_Exception_Returns_500_Without_Leaking_Message()
    {
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);
        var (context, body) = CreateContext("/members");

        var handled = await handler.TryHandleAsync(
            context,
            new InvalidOperationException("internal secret detail"),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);

        var problem = await ReadJsonAsync(body);
        Assert.Equal("An unexpected error occurred.", problem.GetProperty("title").GetString());
        Assert.Equal(500, problem.GetProperty("status").GetInt32());
        Assert.Equal("/members", problem.GetProperty("instance").GetString());

        // Body must not echo the exception's message or stack trace.
        body.Position = 0;
        var raw = await new StreamReader(body).ReadToEndAsync();
        Assert.DoesNotContain("internal secret detail", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", raw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Handler_Always_Claims_Exception_Regardless_Of_Type()
    {
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);
        var (context, _) = CreateContext("/members");

        var handled = await handler.TryHandleAsync(
            context,
            new NotSupportedException("anything"),
            CancellationToken.None);

        Assert.True(handled);
    }

    private static (DefaultHttpContext Context, MemoryStream Body) CreateContext(string path)
    {
        var body = new MemoryStream();
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = body;
        return (context, body);
    }

    private static async Task<JsonElement> ReadJsonAsync(MemoryStream body)
    {
        body.Position = 0;
        using var doc = await JsonDocument.ParseAsync(body);
        return doc.RootElement.Clone();
    }
}
