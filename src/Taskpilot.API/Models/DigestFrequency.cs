namespace Taskpilot.API.Models;

/// <summary>
/// How often a user receives a summary ("digest") email of their task situation.
/// Stored as a string in the database.
/// </summary>
public enum DigestFrequency
{
    /// <summary>No digest emails (default).</summary>
    Off,

    /// <summary>A digest at most once every 24 hours.</summary>
    Daily,

    /// <summary>A digest at most once every 7 days.</summary>
    Weekly,
}
