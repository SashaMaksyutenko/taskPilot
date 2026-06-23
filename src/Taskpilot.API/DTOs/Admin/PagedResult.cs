namespace Taskpilot.API.DTOs.Admin;

/// <summary>
/// A single page of results plus the total count, so the client can render
/// pagination controls (page numbers, "x of y").
/// </summary>
/// <typeparam name="T">Type of the items in the page.</typeparam>
public class PagedResult<T>
{
    /// <summary>The items on the current page.</summary>
    public List<T> Items { get; set; } = new();

    /// <summary>Total number of matching rows across all pages.</summary>
    public int Total { get; set; }

    /// <summary>Current page number (1-based).</summary>
    public int Page { get; set; }

    /// <summary>Number of items per page.</summary>
    public int PageSize { get; set; }
}
