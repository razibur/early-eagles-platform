namespace EarlyEagles.Application.FeatureFlags;

/// <summary>
/// The platform's feature flags (P1-031). The names are the keys under
/// <c>FeatureFlags:Features</c> in configuration, and later in Flagsmith.
/// </summary>
public static class FeatureNames
{
    /// <summary>Sunday sessions: create, list, RSVP, pay (Phase 1).</summary>
    public const string Sessions = "sessions";
}
