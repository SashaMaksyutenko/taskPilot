using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Search;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services.Search;

/// <inheritdoc />
public class SemanticSearchService : ISemanticSearchService
{
    // Bounds on how much of a user's content we embed, to keep a reindex cheap.
    private const int MaxTasks = 200;
    private const int MaxNotes = 100;
    private const int MaxCharsPerItem = 1000;

    private readonly TaskpilotDbContext _context;
    private readonly IEmbeddingClient _embeddings;
    private readonly ILogger<SemanticSearchService> _logger;

    public SemanticSearchService(TaskpilotDbContext context, IEmbeddingClient embeddings, ILogger<SemanticSearchService> logger)
    {
        _context = context;
        _embeddings = embeddings;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsEnabled => _embeddings.IsEnabled;

    /// <inheritdoc />
    public async Task<SemanticStatusDto> GetStatusAsync(Guid userId) => new()
    {
        Enabled = _embeddings.IsEnabled,
        IndexedCount = await _context.SearchDocuments.CountAsync(d => d.OwnerUserId == userId),
    };

    /// <inheritdoc />
    public async Task<Result<ReindexResultDto>> ReindexAsync(Guid userId)
    {
        if (!_embeddings.IsEnabled)
            return Result<ReindexResultDto>.Ok(new ReindexResultDto { Enabled = false, Indexed = 0 });

        // Collect the user's searchable items (tasks they can access + their own notes).
        var tasks = await _context.ProjectTasks
            .Where(t => t.Project.OwnerId == userId || t.Project.Members.Any(m => m.UserId == userId))
            .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
            .Take(MaxTasks)
            .Select(t => new { t.Id, t.ProjectId, t.Title, t.Description })
            .AsNoTracking()
            .ToListAsync();

        var notes = await _context.Notes
            .Where(n => n.OwnerId == userId)
            .OrderByDescending(n => n.UpdatedAt ?? n.CreatedAt)
            .Take(MaxNotes)
            .Select(n => new { n.Id, n.Title, n.Content })
            .AsNoTracking()
            .ToListAsync();

        var items = new List<(string Type, Guid Id, string Title, string Snippet, string Url, string Text)>();
        foreach (var t in tasks)
            items.Add(("Task", t.Id, t.Title, Snippet(t.Description), $"/projects/{t.ProjectId}?task={t.Id}",
                Clip($"{t.Title}\n{t.Description}")));
        foreach (var n in notes)
            items.Add(("Note", n.Id, string.IsNullOrWhiteSpace(n.Title) ? n.Content : n.Title, Snippet(n.Content), "/notes",
                Clip($"{n.Title}\n{n.Content}")));

        // Replace the user's whole index.
        var existing = _context.SearchDocuments.Where(d => d.OwnerUserId == userId);
        _context.SearchDocuments.RemoveRange(existing);

        if (items.Count == 0)
        {
            await _context.SaveChangesAsync();
            return Result<ReindexResultDto>.Ok(new ReindexResultDto { Enabled = true, Indexed = 0 });
        }

        var embedded = await _embeddings.EmbedAsync(items.Select(i => i.Text).ToList());
        if (!embedded.Succeeded)
            return Result<ReindexResultDto>.Fail(embedded.Error!);
        var vectors = embedded.Value!;
        if (vectors.Count != items.Count)
            return Result<ReindexResultDto>.Fail("The embeddings provider returned an unexpected number of vectors.");

        for (var i = 0; i < items.Count; i++)
        {
            var it = items[i];
            _context.SearchDocuments.Add(new SearchDocument
            {
                Id = Guid.NewGuid(),
                OwnerUserId = userId,
                SourceType = it.Type,
                SourceId = it.Id,
                Title = it.Title,
                Snippet = it.Snippet,
                Url = it.Url,
                Embedding = vectors[i],
                UpdatedAt = DateTime.UtcNow,
            });
        }
        await _context.SaveChangesAsync();

        _logger.LogInformation("Semantic index rebuilt for {User}: {Count} items.", userId, items.Count);
        return Result<ReindexResultDto>.Ok(new ReindexResultDto { Enabled = true, Indexed = items.Count });
    }

    /// <inheritdoc />
    public async Task<Result<SemanticSearchResponseDto>> SearchAsync(Guid userId, string query, int limit = 10)
    {
        if (!_embeddings.IsEnabled)
            return Result<SemanticSearchResponseDto>.Ok(new SemanticSearchResponseDto { Enabled = false });

        query = (query ?? string.Empty).Trim();
        if (query.Length == 0)
            return Result<SemanticSearchResponseDto>.Ok(new SemanticSearchResponseDto { Enabled = true });
        if (limit is < 1 or > 50) limit = 10;

        var embedded = await _embeddings.EmbedAsync(new[] { query });
        if (!embedded.Succeeded || embedded.Value!.Count == 0)
            return Result<SemanticSearchResponseDto>.Fail(embedded.Error ?? "Could not embed the query.");
        var queryVec = embedded.Value![0];

        var docs = await _context.SearchDocuments
            .Where(d => d.OwnerUserId == userId)
            .Select(d => new { d.SourceType, d.SourceId, d.Title, d.Snippet, d.Url, d.Embedding })
            .AsNoTracking()
            .ToListAsync();

        var results = docs
            .Select(d => new SemanticSearchResultDto
            {
                SourceType = d.SourceType,
                SourceId = d.SourceId,
                Title = d.Title,
                Snippet = d.Snippet,
                Url = d.Url,
                Score = Cosine(queryVec, d.Embedding),
            })
            .Where(r => r.Score > 0)
            .OrderByDescending(r => r.Score)
            .Take(limit)
            .ToList();

        return Result<SemanticSearchResponseDto>.Ok(new SemanticSearchResponseDto { Enabled = true, Results = results });
    }

    /// <summary>Cosine similarity of two vectors; 0 when either is empty/zero.</summary>
    private static double Cosine(float[] a, float[] b)
    {
        var n = Math.Min(a.Length, b.Length);
        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < n; i++)
        {
            dot += (double)a[i] * b[i];
            na += (double)a[i] * a[i];
            nb += (double)b[i] * b[i];
        }
        return na == 0 || nb == 0 ? 0 : dot / (Math.Sqrt(na) * Math.Sqrt(nb));
    }

    private static string Clip(string text) =>
        text.Length <= MaxCharsPerItem ? text : text[..MaxCharsPerItem];

    private static string Snippet(string? text)
    {
        text = (text ?? string.Empty).Replace('\n', ' ').Replace('\r', ' ').Trim();
        return text.Length <= 160 ? text : text[..160] + "…";
    }
}
