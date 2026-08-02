using Taskpilot.API.Common;

namespace Taskpilot.API.Services.Search;

/// <summary>Turns text into embedding vectors for semantic search.</summary>
public interface IEmbeddingClient
{
    /// <summary>True only when an embeddings provider is configured.</summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Embeds a batch of texts, returning one vector per input in the same order.
    /// Fails when the provider is not configured or the call errors.
    /// </summary>
    Task<Result<List<float[]>>> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken ct = default);
}
