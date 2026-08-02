namespace Taskpilot.API.Models;

/// <summary>
/// A pre-computed embedding of one searchable item (a task or note), scoped to the user who
/// can see it. Semantic search embeds the query and ranks these by cosine similarity. The
/// index is rebuilt per user on demand; the vector is stored as a plain float array (no
/// pgvector — cosine is computed in-app, which is fine for this data size).
/// </summary>
public class SearchDocument
{
    public Guid Id { get; set; }

    /// <summary>User whose personal index this document belongs to (foreign key).</summary>
    public Guid OwnerUserId { get; set; }

    /// <summary>Navigation to the owner.</summary>
    public User OwnerUser { get; set; } = null!;

    /// <summary>Kind of the indexed item ("Task" | "Note").</summary>
    public string SourceType { get; set; } = string.Empty;

    /// <summary>Id of the indexed item.</summary>
    public Guid SourceId { get; set; }

    /// <summary>Display title for the result.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Short text preview for the result.</summary>
    public string Snippet { get; set; } = string.Empty;

    /// <summary>Frontend link to open the item.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>The embedding vector of the item's text.</summary>
    public float[] Embedding { get; set; } = Array.Empty<float>();

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
