namespace Taskpilot.API.Models;

/// <summary>
/// The context a peer <see cref="Review"/> was left in — what the two users' relationship was
/// when the review was written. Kept as a small enum so a single Reviews table (and a single
/// reputation aggregate) covers every surface.
/// </summary>
public enum ReviewContext
{
    /// <summary>A review between the poster and assignee of a completed marketplace task.</summary>
    Marketplace = 0,

    /// <summary>A review between two members of the same project.</summary>
    Project = 1,

    /// <summary>A review tied to a forum interaction (e.g. an accepted solution).</summary>
    Forum = 2,
}
