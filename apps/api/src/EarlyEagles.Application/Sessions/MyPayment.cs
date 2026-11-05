using System.Text.Json.Serialization;
using EarlyEagles.Domain.Payments;

namespace EarlyEagles.Application.Sessions;

/// <summary>Where the member's own payment stands; the reason is there when it was rejected.</summary>
public sealed record MyPayment(
    [property: JsonConverter(typeof(JsonStringEnumConverter<PaymentStatus>))] PaymentStatus Status,
    int AmountCents,
    string? RejectionReason);
