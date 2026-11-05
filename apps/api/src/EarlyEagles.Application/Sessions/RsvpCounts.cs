namespace EarlyEagles.Application.Sessions;

/// <summary>How many members gave each answer.</summary>
public sealed record RsvpCounts(int Yes, int No, int Maybe);
