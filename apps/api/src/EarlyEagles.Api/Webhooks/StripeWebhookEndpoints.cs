using System.Text;
using EarlyEagles.Infrastructure.Billing;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace EarlyEagles.Api.Webhooks;

/// <summary>
/// <c>POST /api/webhooks/stripe</c> — Stripe's webhook deliveries for tenant
/// subscription billing (P0-043.1), accepted only with a valid
/// <c>Stripe-Signature</c> over the raw body (P0-044).
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>Anonymous: the signature is the authentication, checked over the
///   body exactly as received, before anything parses it.</item>
///   <item>Tenant-agnostic: Stripe calls the bare API host (the path is on
///   <see cref="Tenancy.TenantResolutionMiddleware"/>'s allow-list). The
///   tenant will come from the Stripe customer (P0-045.2).</item>
///   <item>Any 2xx ends the delivery; anything else is retried for up to
///   three days. Every verified event is acknowledged (200): handlers arrive
///   with P0-043.2–.4.</item>
///   <item>Not in the OpenAPI document: it's an integration endpoint.</item>
/// </list>
/// </remarks>
public static class StripeWebhookEndpoints
{
    public const string Route = "/api/webhooks/stripe";

    public const string SignatureHeader = "Stripe-Signature";

    private const string LoggerCategory = "EarlyEagles.Api.Webhooks.StripeWebhook";

    public static IEndpointRouteBuilder MapStripeWebhookEndpoints(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        routes
            .MapPost(Route, ReceiveAsync)
            .WithName("ReceiveStripeWebhook")
            .ExcludeFromDescription();

        return routes;
    }

    private static async Task<Results<Ok, ProblemHttpResult>> ReceiveAsync(
        HttpRequest request,
        IOptions<StripeOptions> options,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(LoggerCategory);
        var secret = options.Value.WebhookSigningSecret;
        if (string.IsNullOrEmpty(secret))
        {
            StripeWebhookLog.NotConfigured(logger);
            return TypedResults.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Stripe webhooks not configured",
                detail: "This environment has no Stripe webhook signing secret.");
        }

        var body = await WebhookBody.ReadAsync(request, cancellationToken).ConfigureAwait(false);
        if (body is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "Webhook payload too large");
        }

        // Stripe signs the UTF-8 body; bytes that aren't UTF-8 decode to
        // replacement characters, change, and fail the check.
        var result = new StripeWebhookVerifier(secret, timeProvider).Verify(
            Encoding.UTF8.GetString(body),
            request.Headers[SignatureHeader].ToString(),
            out var stripeEvent);
        switch (result)
        {
            case StripeWebhookVerificationResult.Valid:
                StripeWebhookLog.Received(logger, stripeEvent!.Type, stripeEvent.Id, stripeEvent.Livemode);
                return TypedResults.Ok();

            case StripeWebhookVerificationResult.ApiVersionMismatch:
                // Signed by Stripe but unreadable until the endpoint's API
                // version is fixed. 400 keeps Stripe retrying meanwhile.
                StripeWebhookLog.ApiVersionMismatch(logger, Stripe.StripeConfiguration.ApiVersion);
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Unsupported Stripe API version",
                    detail: $"This endpoint reads events in Stripe API version {Stripe.StripeConfiguration.ApiVersion}.");

            case StripeWebhookVerificationResult.Malformed:
                StripeWebhookLog.Malformed(logger);
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Malformed webhook payload",
                    detail: "Expected a Stripe event object.");

            default:
                // The reason is logged, not returned: a caller without the
                // secret learns nothing about why it failed.
                StripeWebhookLog.Rejected(logger, result);
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid webhook signature");
        }
    }
}
