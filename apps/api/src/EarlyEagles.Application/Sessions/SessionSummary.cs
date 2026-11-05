using System.Text.Json.Serialization;
using EarlyEagles.Domain.Sessions;

namespace EarlyEagles.Application.Sessions;

/// <summary>A session as a list shows it (P1-005).</summary>
public sealed record SessionSummary(
    Guid Id,
    DateOnly Date,
    TimeOnly StartTime,
    string Venue,
    int FeeCents,
    [property: JsonConverter(typeof(JsonStringEnumConverter<SessionStatus>))] SessionStatus Status);
