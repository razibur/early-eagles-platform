using EarlyEagles.Application.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace EarlyEagles.Api.ExceptionHandling;

/// <summary>
/// Maps the Application layer's refusals to their HTTP status:
/// <see cref="NotFoundException"/> to 404, <see cref="ConflictException"/> to 409.
/// </summary>
public sealed class ApplicationExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        var status = exception switch
        {
            NotFoundException => StatusCodes.Status404NotFound,
            ConflictException => StatusCodes.Status409Conflict,
            _ => 0,
        };
        if (status == 0)
        {
            return false;
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = exception.Message,
            Type = "https://datatracker.ietf.org/doc/html/rfc7807#section-3.1",
            Instance = httpContext.Request.Path,
        };

        httpContext.Response.StatusCode = status;
        await httpContext.Response
            .WriteAsJsonAsync(problem, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return true;
    }
}
