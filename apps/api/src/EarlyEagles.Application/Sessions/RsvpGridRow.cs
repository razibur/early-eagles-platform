using System.Text.Json.Serialization;
using EarlyEagles.Domain.Sessions;

namespace EarlyEagles.Application.Sessions;

/// <summary>One member in the admin's RSVP grid (P1-007.2). <c>Response</c> is <c>null</c> until they answer.</summary>
public sealed record RsvpGridRow(
    string ClerkUserId,
    string? DisplayName,
    string? ImageUrl,
    [property: JsonConverter(typeof(JsonStringEnumConverter<RsvpResponse>))] RsvpResponse? Response);
