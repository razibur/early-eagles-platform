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

        // P1-009: a tenant admin confirms a member's payment.
        routes
            .MapPost("/sessions/{id:guid}/payments/{memberId}/confirm", ConfirmPayment)
            .RequireAuthorization(TenantPolicies.Admin)
            .RequireFeature(FeatureNames.Sessions)
            .WithName("ConfirmPayment")
            .WithTags("Payments");

        // P1-009.2: a tenant admin rejects a member's payment with a reason.
        routes
            .MapPost("/sessions/{id:guid}/payments/{memberId}/reject", RejectPayment)
            .RequireAuthorization(TenantPolicies.Admin)
            .RequireFeature(FeatureNames.Sessions)
            .WithName("RejectPayment")
            .WithTags("Payments");

        // P1-009.3: a tenant admin reads who still needs dealing with.
        routes
            .MapGet("/sessions/{id:guid}/payments", GetUnsettledPayments)
            .RequireAuthorization(TenantPolicies.Admin)
            .RequireFeature(FeatureNames.Sessions)
            .WithName("GetUnsettledPayments")
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

    private static async Task<Ok<IReadOnlyList<UnsettledPaymentRow>>> GetUnsettledPayments(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var rows = await sender.Send(new GetUnsettledPaymentsQuery(id), cancellationToken);
        return TypedResults.Ok(rows);
    }

    private static async Task<NoContent> RejectPayment(
        Guid id,
        string memberId,
        RejectPaymentRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new RejectPaymentCommand(id, memberId, user.FindFirstValue(ClerkClaims.Subject) ?? string.Empty, request.Reason ?? string.Empty),
            cancellationToken);

        return TypedResults.NoContent();
    }

    private static async Task<NoContent> ConfirmPayment(
        Guid id,
        string memberId,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new ConfirmPaymentCommand(id, memberId, user.FindFirstValue(ClerkClaims.Subject) ?? string.Empty),
            cancellationToken);

        return TypedResults.NoContent();
    }
}
