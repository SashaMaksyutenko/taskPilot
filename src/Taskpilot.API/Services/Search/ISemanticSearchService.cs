using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Search;

namespace Taskpilot.API.Services.Search;

/// <summary>Embedding-based semantic search over a user's tasks and notes.</summary>
public interface ISemanticSearchService
{
    /// <summary>True only when an embeddings provider is configured.</summary>
    bool IsEnabled { get; }

    /// <summary>Rebuilds the user's semantic index from their current tasks and notes.</summary>
    Task<Result<ReindexResultDto>> ReindexAsync(Guid userId);

    /// <summary>Ranks the user's indexed items by semantic similarity to the query.</summary>
    Task<Result<SemanticSearchResponseDto>> SearchAsync(Guid userId, string query, int limit = 10);

    /// <summary>Whether the feature is enabled and how many items the user has indexed.</summary>
    Task<SemanticStatusDto> GetStatusAsync(Guid userId);
}
