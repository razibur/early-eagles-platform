using System.Text.Json.Serialization;
using EarlyEagles.Domain.Payments;
using EarlyEagles.Domain.Sessions;

namespace EarlyEagles.Application.Sessions;

/// <summary>
/// A session as its page shows it, from one member's point of view (P1-005.2).
/// <c>MyRsvp</c> and <c>MyPayment</c> are <c>null</c> until that member acts.
/// </summary>
public sealed record SessionDetails(
    Guid Id,
    DateOnly Date,
    TimeOnly StartTime,
    string Venue,
    int FeeCents,
    [property: JsonConverter(typeof(JsonStringEnumConverter<SessionStatus>))] SessionStatus Status,
    RsvpCounts RsvpCounts,
    [property: JsonConverter(typeof(JsonStringEnumConverter<RsvpResponse>))] RsvpResponse? MyRsvp,
    MyPayment? MyPayment);
