using System.Text.Json;
using EarlyEagles.Infrastructure.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace EarlyEagles.Api.Webhooks;

/// <summary>
/// <c>POST /api/webhooks/clerk</c> — Clerk's webhook deliveries (P0-036.1),
/// accepted only with a valid Svix signature over the raw body (P0-037).
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>Anonymous: the signature is the authentication. It's verified over
///   the exact bytes received, before anything parses them.</item>
///   <item>Tenant-agnostic: Clerk calls the bare API host, so the path is on
///   <see cref="Tenancy.TenantResolutionMiddleware"/>'s allow-list. The tenant
///   comes from the payload (an organization id); see
///   <see cref="ClerkWebhookDispatcher"/>.</item>
///   <item>Any 2xx tells Svix the delivery succeeded; anything else is retried
///   with backoff. Event types with no handler yet are acknowledged (200) so
///   they aren't retried for days. An event that arrived ahead of the one it
///   depends on answers 503, so it is retried.</item>
///   <item>Not in the OpenAPI document: it's an integration endpoint, not
///   something API clients call.</item>
/// </list>
/// </remarks>
public static class ClerkWebhookEndpoints
{
    public const string Route = "/api/webhooks/clerk";

    /// <summary>Clerk events are a few KB; anything this size is not one.</summary>
    public const int MaxBodyBytes = WebhookBody.MaxBytes;

    private const string LoggerCategory = "EarlyEagles.Api.Webhooks.ClerkWebhook";

    public static IEndpointRouteBuilder MapClerkWebhookEndpoints(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        routes
            .MapPost(Route, ReceiveAsync)
            .WithName("ReceiveClerkWebhook")
            .ExcludeFromDescription();

        return routes;
    }

    private static async Task<Results<Ok, ProblemHttpResult>> ReceiveAsync(
        HttpRequest request,
        IOptions<ClerkOptions> options,
        TimeProvider timeProvider,
        ClerkWebhookDispatcher dispatcher,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(LoggerCategory);
        var secret = options.Value.WebhookSigningSecret;
        if (string.IsNullOrEmpty(secret))
        {
            ClerkWebhookLog.NotConfigured(logger);
            return TypedResults.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Clerk webhooks not configured",
                detail: "This environment has no Clerk webhook signing secret.");
        }

        var body = await WebhookBody.ReadAsync(request, cancellationToken).ConfigureAwait(false);
        if (body is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "Webhook payload too large");
        }

        var messageId = request.Headers["svix-id"].ToString();
        var result = new SvixWebhookVerifier(secret, timeProvider).Verify(
            messageId,
            request.Headers["svix-timestamp"].ToString(),
            request.Headers["svix-signature"].ToString(),
            body);
        if (result != SvixVerificationResult.Valid)
        {
            // The reason is logged, not returned: a caller without the secret
            // learns nothing about why it failed.
            ClerkWebhookLog.Rejected(logger, result, messageId);
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid webhook signature");
        }

        using var document = TryParse(body);
        if (document is null || !TryReadEventType(document.RootElement, out var eventType))
        {
            ClerkWebhookLog.Malformed(logger, messageId);
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Malformed webhook payload",
                detail: "Expected a JSON object with a string 'type'.");
        }

        ClerkWebhookLog.Received(logger, eventType, messageId);
        var data = document.RootElement.TryGetProperty("data", out var value) ? value : default;
        var outcome = await dispatcher.DispatchAsync(eventType, data, messageId, cancellationToken).ConfigureAwait(false);
        if (outcome == ClerkWebhookOutcome.RetryLater)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Webhook deferred",
                detail: "The event depends on one not processed yet; retry later.");
        }

        return TypedResults.Ok();
    }

    private static JsonDocument? TryParse(byte[] body)
    {
        try
        {
            return JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryReadEventType(JsonElement root, out string eventType)
    {
        eventType = string.Empty;
        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("type", out var type)
            && type.ValueKind == JsonValueKind.String
            && !string.IsNullOrEmpty(type.GetString()))
        {
            eventType = type.GetString()!;
            return true;
        }

        return false;
    }
}
