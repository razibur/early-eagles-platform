namespace EarlyEagles.Api.Payments;

/// <summary>The body of <c>POST /sessions/{id}/payment-report</c>: the name on the transfer and its Zelle reference.</summary>
public sealed record ReportPaymentRequest(string? SenderName, string? Reference);
