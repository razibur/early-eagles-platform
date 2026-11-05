namespace EarlyEagles.Api.Payments;

/// <summary>The body of the reject endpoint: why, in words the member will read.</summary>
public sealed record RejectPaymentRequest(string? Reason);
