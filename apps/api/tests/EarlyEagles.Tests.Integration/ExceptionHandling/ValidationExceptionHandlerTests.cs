using System.Text.Json;
using EarlyEagles.Api.ExceptionHandling;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;

namespace EarlyEagles.Tests.Integration.ExceptionHandling;

public class ValidationExceptionHandlerTests
{
    [Fact]
    public async Task ValidationException_Returns_400_With_Grouped_Errors()
    {
        var handler = new ValidationExceptionHandler();
        var failures = new List<ValidationFailure>
        {
            new("Email", "Email is required"),
            new("Email", "Email must be a valid format"),
            new("Name", "Name is required"),
        };
        var (context, body) = CreateContext("/members");

        var handled = await handler.TryHandleAsync(
            context,
            new ValidationException(failures),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);

        var problem = await ReadJsonAsync(body);
        Assert.Equal("One or more validation errors occurred.", problem.GetProperty("title").GetString());
        Assert.Equal(400, problem.GetProperty("status").GetInt32());
        Assert.Equal("/members", problem.GetProperty("instance").GetString());

        var errors = problem.GetProperty("errors");
        Assert.Equal(2, errors.EnumerateObject().Count(p => p.Name is "Email" or "Name"));
        Assert.Equal(2, errors.GetProperty("Email").GetArrayLength());
        Assert.Equal("Email is required", errors.GetProperty("Email")[0].GetString());
        Assert.Equal("Name is required", errors.GetProperty("Name")[0].GetString());
    }

    [Fact]
    public async Task Non_Validation_Exception_Is_Not_Claimed()
    {
        var handler = new ValidationExceptionHandler();
        var (context, body) = CreateContext("/members");

        var handled = await handler.TryHandleAsync(
            context,
            new InvalidOperationException("oops"),
            CancellationToken.None);

        Assert.False(handled);
        Assert.Equal(0, body.Length);
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
