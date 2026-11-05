using System.Security.Claims;
using EarlyEagles.Api.Authentication;
using EarlyEagles.Api.FeatureFlags;
using EarlyEagles.Application.FeatureFlags;
using EarlyEagles.Application.Payments;
using Mediator;
using Microsoft.AspNetCore.Http.HttpResults;

namespace EarlyEagles.Api.Payments;

/// <summary>
/// Reporting and reviewing session payments, behind the sessions flag: 404
/// while it is off for this club, 401 without a token, 403 outside the policy.
/// Refusals by state are 409.
/// </summary>
public static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        // P1-008: a member reports their own payment.
        routes
            .MapPost("/sessions/{id:guid}/payment-report", ReportPayment)
            .RequireAuthorization(TenantPolicies.Member)
            .RequireFeature(FeatureNames.Sessions)
            .WithName("ReportPayment")
            .WithTags("Payments");

        return routes;
    }

    private static async Task<Ok<ReportPaymentResponse>> ReportPayment(
        Guid id,
        ReportPaymentRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ReportPaymentCommand(
                id,
                user.FindFirstValue(ClerkClaims.Subject) ?? string.Empty,
                request.SenderName ?? string.Empty,
                request.Reference ?? string.Empty),
            cancellationToken);

        return TypedResults.Ok(new ReportPaymentResponse(result.PaymentId));
    }
}
