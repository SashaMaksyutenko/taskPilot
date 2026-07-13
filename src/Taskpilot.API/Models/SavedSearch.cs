namespace Taskpilot.API.Models;

/// <summary>
/// A search query a user saved for quick re-running from the global search page.
/// </summary>
public class SavedSearch
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Owner of the saved search (foreign key).</summary>
    public Guid UserId { get; set; }

    /// <summary>Navigation to the owner.</summary>
    public User User { get; set; } = null!;

    /// <summary>Display name the user gave the saved search.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The search query text to re-run.</summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>UTC time the search was saved.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
