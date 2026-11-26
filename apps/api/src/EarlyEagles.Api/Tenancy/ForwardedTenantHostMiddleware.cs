using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace EarlyEagles.Api.Tenancy;

/// <summary>
/// Lets the web app's <c>/api/*</c> forwarding keep the club (P0-071.4,
/// ADR-0016). The browser calls <c>alpha.earlyeagles.club/api/me</c>; the web
/// app forwards it here, where <c>Host</c> is the API's own name. When the
/// request carries the shared <see cref="TrustedProxyOptions.Key"/>, this sets
/// <c>Request.Host</c> to the <c>X-Forwarded-Host</c> the web app sent, so
/// <see cref="TenantResolutionMiddleware"/> (next in the pipeline) finds
/// <c>alpha</c>. Without the key, or with a wrong one, the forwarded host is
/// ignored and the request goes on with its own <c>Host</c>.
/// <para>The key header is removed from every request, so nothing downstream
/// (logs, traces, handlers) ever sees it.</para>
/// </summary>
public sealed partial class ForwardedTenantHostMiddleware : IMiddleware
{
    private readonly byte[]? key;
    private readonly ILogger<ForwardedTenantHostMiddleware> logger;

    public ForwardedTenantHostMiddleware(IOptions<TrustedProxyOptions> options, ILogger<ForwardedTenantHostMiddleware> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        var configured = options.Value.Key;
        key = string.IsNullOrEmpty(configured) ? null : Encoding.UTF8.GetBytes(configured);
        this.logger = logger;
    }

    /// <summary>
    /// Exactly one DNS name, optionally with a port (<c>alpha.earlyeagles.club</c>,
    /// <c>alpha.localhost:3002</c>): what the web app forwards. A list
    /// (<c>a, b</c>, from a chain of proxies), an IP literal or anything else
    /// is refused; the web app is the only proxy.
    /// </summary>
    public static bool TryParseForwardedHost(Microsoft.Extensions.Primitives.StringValues values, out HostString host)
    {
        host = default;
        if (values.Count != 1 || values[0] is not { Length: > 0 and <= 260 } value)
        {
            return false;
        }

        var match = ForwardedHostPattern().Match(value);
        if (!match.Success)
        {
            return false;
        }

        var port = match.Groups["port"];
        if (port.Success && int.Parse(port.ValueSpan, CultureInfo.InvariantCulture) is < 1 or > 65535)
        {
            return false;
        }

        host = new HostString(value);
        return true;
    }

    public Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var request = context.Request;
        var supplied = request.Headers[TrustedProxyOptions.KeyHeader];
        request.Headers.Remove(TrustedProxyOptions.KeyHeader);

        if (supplied.Count > 0)
        {
            if (key is null || supplied.Count != 1 || !Matches(supplied[0]))
            {
                LogKeyRejected(logger);
            }
            else if (TryParseForwardedHost(request.Headers[TrustedProxyOptions.ForwardedHostHeader], out var host))
            {
                request.Host = host;
            }
            else
            {
                LogForwardedHostRejected(logger);
            }
        }

        return next(context);
    }

    [GeneratedRegex(@"^(?=[^:]*[a-zA-Z])[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*(?::(?<port>[0-9]{1,5}))?$", RegexOptions.CultureInvariant)]
    private static partial Regex ForwardedHostPattern();

    [LoggerMessage(EventId = 1700, Level = LogLevel.Warning, Message = "Forwarded request refused: the proxy key is missing, wrong or not configured")]
    private static partial void LogKeyRejected(ILogger logger);

    [LoggerMessage(EventId = 1701, Level = LogLevel.Warning, Message = "Forwarded request carried the proxy key but no single valid X-Forwarded-Host")]
    private static partial void LogForwardedHostRejected(ILogger logger);

    private bool Matches(string? candidate) =>
        candidate is not null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(candidate), key);
}
