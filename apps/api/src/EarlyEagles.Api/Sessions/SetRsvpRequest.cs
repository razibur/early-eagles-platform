using System.Text.Json.Serialization;
using EarlyEagles.Domain.Sessions;

namespace EarlyEagles.Api.Sessions;

/// <summary>The body of <c>PUT /sessions/{id}/rsvp</c>: Yes, No or Maybe.</summary>
public sealed record SetRsvpRequest(
    [property: JsonConverter(typeof(JsonStringEnumConverter<RsvpResponse>))] RsvpResponse? Response);
