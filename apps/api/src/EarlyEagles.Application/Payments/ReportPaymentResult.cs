namespace EarlyEagles.Application.Payments;

/// <summary>The payment's id, the same one when reporting again after a rejection.</summary>
public sealed record ReportPaymentResult(Guid PaymentId);
