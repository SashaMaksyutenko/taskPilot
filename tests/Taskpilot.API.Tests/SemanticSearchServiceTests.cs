using Microsoft.Extensions.Logging.Abstractions;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.Models;
using Taskpilot.API.Services.Search;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Tests for semantic search with a FAKE embedder (deterministic keyword-count vectors), so
/// ranking, per-user scoping and the disabled path are verified without a real provider.
/// </summary>
public class SemanticSearchServiceTests
{
    /// <summary>Maps text to a vector counting a fixed vocabulary — enough to assert ranking.</summary>
    private sealed class FakeEmbedder : IEmbeddingClient
    {
        private static readonly string[] Vocab = { "login", "payment", "design" };
        public bool IsEnabled { get; set; } = true;

        public Task<Result<List<float[]>>> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken ct = default)
        {
            if (!IsEnabled) return Task.FromResult(Result<List<float[]>>.Fail("disabled"));
            var vecs = inputs
                .Select(text => Vocab.Select(w => (float)Count(text.ToLowerInvariant(), w)).ToArray())
                .ToList();
            return Task.FromResult(Result<List<float[]>>.Ok(vecs));
        }

        private static int Count(string s, string w)
        {
            int c = 0, i = 0;
            while ((i = s.IndexOf(w, i, StringComparison.Ordinal)) >= 0) { c++; i += w.Length; }
            return c;
        }
    }

    private static SemanticSearchService Make(TaskpilotDbContext ctx, IEmbeddingClient embedder) =>
        new(ctx, embedder, NullLogger<SemanticSearchService>.Instance);

    private static async Task<Guid> SeedTaskAsync(TaskpilotDbContext ctx, Guid owner, Guid projectId, string title)
    {
        var id = Guid.NewGuid();
        ctx.ProjectTasks.Add(new ProjectTask { Id = id, ProjectId = projectId, CreatorId = owner, Title = title });
        await ctx.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task Reindex_ThenSearch_RanksBySimilarity()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "U");
        var project = await TestDb.AddProjectAsync(ctx, user, "P");
        var loginTask = await SeedTaskAsync(ctx, user, project, "Fix login bug");
        await SeedTaskAsync(ctx, user, project, "Payment failed on checkout");
        await SeedTaskAsync(ctx, user, project, "Redesign the homepage design");
        var svc = Make(ctx, new FakeEmbedder());

        var reindex = await svc.ReindexAsync(user);
        Assert.True(reindex.Value!.Enabled);
        Assert.Equal(3, reindex.Value.Indexed);

        var results = (await svc.SearchAsync(user, "cannot login")).Value!.Results;
        Assert.NotEmpty(results);
        Assert.Equal(loginTask, results[0].SourceId); // closest by meaning
        Assert.Equal("Task", results[0].SourceType);
    }

    [Fact]
    public async Task Search_And_Reindex_AreDisabled_WithoutProvider()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "U");
        var svc = Make(ctx, new FakeEmbedder { IsEnabled = false });

        Assert.False(svc.IsEnabled);
        var search = await svc.SearchAsync(user, "anything");
        Assert.True(search.Succeeded);
        Assert.False(search.Value!.Enabled);
        Assert.Empty(search.Value.Results);

        var reindex = await svc.ReindexAsync(user);
        Assert.False(reindex.Value!.Enabled);
        Assert.Equal(0, reindex.Value.Indexed);
    }

    [Fact]
    public async Task Index_IsScopedPerUser()
    {
        await using var ctx = TestDb.CreateContext();
        var a = await TestDb.AddUserAsync(ctx, "A");
        var b = await TestDb.AddUserAsync(ctx, "B");
        var project = await TestDb.AddProjectAsync(ctx, a, "P");
        await SeedTaskAsync(ctx, a, project, "Fix login bug");
        var svc = Make(ctx, new FakeEmbedder());

        await svc.ReindexAsync(a);
        await svc.ReindexAsync(b); // B has no access to A's project → nothing to index

        Assert.Equal(1, (await svc.GetStatusAsync(a)).IndexedCount);
        Assert.Equal(0, (await svc.GetStatusAsync(b)).IndexedCount);
        Assert.NotEmpty((await svc.SearchAsync(a, "login")).Value!.Results);
        Assert.Empty((await svc.SearchAsync(b, "login")).Value!.Results);
    }
}
