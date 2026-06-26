namespace Taskpilot.API.DTOs.Search;

/// <summary>One search hit. <see cref="Id"/> is the navigation target for its category.</summary>
public class SearchItemDto
{
    /// <summary>Id used to build the link (project id for tasks/projects, topic id, user id).</summary>
    public Guid Id { get; set; }

    /// <summary>Primary text to show.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Optional secondary text (e.g. the project a task belongs to).</summary>
    public string? Sublabel { get; set; }
}

/// <summary>Grouped results of a global search.</summary>
public class SearchResultsDto
{
    public List<SearchItemDto> Projects { get; set; } = new();
    public List<SearchItemDto> Tasks { get; set; } = new();
    public List<SearchItemDto> Topics { get; set; } = new();
    public List<SearchItemDto> Users { get; set; } = new();
}
