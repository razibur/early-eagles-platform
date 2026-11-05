using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace EarlyEagles.Api.ExceptionHandling;

/// <summary>
/// Answers a body the framework couldn't read (malformed JSON, a date that
/// doesn't parse) with the status it chose. Minimal APIs only throw
/// <see cref="BadHttpRequestException"/> in Development; elsewhere they answer
/// 400 themselves. Without this handler Development would report a 500.
/// </summary>
public sealed class BadRequestExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (exception is not BadHttpRequestException badRequest)
        {
            return false;
        }

        var problem = new ProblemDetails
        {
            Status = badRequest.StatusCode,
            Title = "The request could not be read.",
            Type = "https://datatracker.ietf.org/doc/html/rfc7807#section-3.1",
            Instance = httpContext.Request.Path,
        };

        httpContext.Response.StatusCode = badRequest.StatusCode;
        await httpContext.Response
            .WriteAsJsonAsync(problem, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return true;
    }
}
