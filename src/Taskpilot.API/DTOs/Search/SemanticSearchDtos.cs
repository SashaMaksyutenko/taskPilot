namespace Taskpilot.API.DTOs.Search;

/// <summary>One semantic-search hit.</summary>
public class SemanticSearchResultDto
{
    public string SourceType { get; set; } = string.Empty;
    public Guid SourceId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;

    /// <summary>Cosine similarity to the query, 0–1 (higher is closer).</summary>
    public double Score { get; set; }
}

/// <summary>A semantic-search response, including whether the feature is enabled.</summary>
public class SemanticSearchResponseDto
{
    /// <summary>False when no embeddings provider is configured (use keyword search instead).</summary>
    public bool Enabled { get; set; }
    public List<SemanticSearchResultDto> Results { get; set; } = new();
}

/// <summary>Status of a user's semantic index.</summary>
public class SemanticStatusDto
{
    public bool Enabled { get; set; }

    /// <summary>Number of items currently indexed for the user.</summary>
    public int IndexedCount { get; set; }
}

/// <summary>Result of rebuilding a user's index.</summary>
public class ReindexResultDto
{
    public bool Enabled { get; set; }

    /// <summary>Number of items indexed.</summary>
    public int Indexed { get; set; }
}
