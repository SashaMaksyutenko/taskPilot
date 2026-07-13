namespace Taskpilot.API.DTOs.Search;

/// <summary>Body for saving a search query.</summary>
public class CreateSavedSearchDto
{
    /// <summary>Display name for the saved search.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The query text to save.</summary>
    public string Query { get; set; } = string.Empty;
}

/// <summary>A saved search shaped for the client.</summary>
public class SavedSearchDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
