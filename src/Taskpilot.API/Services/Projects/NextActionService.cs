using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Planning;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// "What should I do next?" — gathers the user's open, assigned tasks, orders them by a simple
/// urgency heuristic, and (when the LLM is configured) asks the model to re-rank them and explain
/// each choice. Falls back to the deterministic order when there's no key or the model stalls.
/// Works with the same config-gated LLM as the weekly digest.
/// </summary>
public partial class NextActionService : INextActionService
{
    // How many tasks we hand the model to choose from (bounds the prompt).
    private const int CandidatePool = 15;

    private readonly TaskpilotDbContext _context;
    private readonly IChatBotClient _llm;
    private readonly ILogger<NextActionService> _logger;

    public NextActionService(TaskpilotDbContext context, IChatBotClient llm, ILogger<NextActionService> logger)
    {
        _context = context;
        _llm = llm;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsEnabled => _llm.IsEnabled;

    /// <inheritdoc />
    public async Task<NextActionsDto> GetPlanAsync(Guid userId, int limit = 8)
    {
        if (limit < 1) limit = 1;
        var now = DateTime.UtcNow;

        // Open tasks assigned to the user in a project they can access and that isn't archived.
        var candidates = await _context.ProjectTasks
            .Where(t => t.AssigneeId == userId
                        && t.Status != ProjectTaskStatus.Done
                        && t.Project.ArchivedAt == null
                        && (t.Project.OwnerId == userId || t.Project.Members.Any(m => m.UserId == userId)))
            .Select(t => new NextActionItemDto
            {
                TaskId = t.Id,
                ProjectId = t.ProjectId,
                Number = t.Number,
                Title = t.Title,
                ProjectName = t.Project.Name,
                ProjectColor = t.Project.Color,
                Priority = t.Priority.ToString(),
                Deadline = t.Deadline,
                IsOverdue = t.Deadline != null && t.Deadline < now,
            })
            .ToListAsync();

        if (candidates.Count == 0)
            return new NextActionsDto { Enabled = _llm.IsEnabled, RankedByAi = false };

        // Flag tasks blocked by an unfinished dependency (they can't be started yet).
        var candidateIds = candidates.Select(c => c.TaskId).ToList();
        var blockedIds = await _context.TaskDependencies
            .Where(d => candidateIds.Contains(d.TaskId)
                        && _context.ProjectTasks.Any(bt => bt.Id == d.DependsOnTaskId && bt.Status != ProjectTaskStatus.Done))
            .Select(d => d.TaskId)
            .Distinct()
            .ToListAsync();
        var blocked = blockedIds.ToHashSet();
        foreach (var c in candidates) c.IsBlocked = blocked.Contains(c.TaskId);

        // Deterministic urgency order: startable first, then overdue, then by deadline (nulls last),
        // then higher priority, then oldest. This is both the LLM's candidate list and the fallback.
        var ordered = candidates
            .OrderBy(c => c.IsBlocked)
            .ThenByDescending(c => c.IsOverdue)
            .ThenBy(c => c.Deadline ?? DateTime.MaxValue)
            .ThenByDescending(c => PriorityRank(c.Priority))
            .ThenBy(c => c.Number)
            .ToList();

        var pool = ordered.Take(CandidatePool).ToList();

        if (!_llm.IsEnabled)
            return new NextActionsDto { Enabled = false, RankedByAi = false, Items = pool.Take(limit).ToList() };

        var ranked = await RankWithAiAsync(pool, limit, now);
        return ranked
            ?? new NextActionsDto { Enabled = true, RankedByAi = false, Items = pool.Take(limit).ToList() };
    }

    /// <summary>Asks the LLM to order the candidate pool and explain each pick; null on any failure.</summary>
    private async Task<NextActionsDto?> RankWithAiAsync(List<NextActionItemDto> pool, int limit, DateTime now)
    {
        var lines = new StringBuilder();
        for (var i = 0; i < pool.Count; i++)
        {
            var c = pool[i];
            var due = c.Deadline is null
                ? "no deadline"
                : $"due {c.Deadline.Value:yyyy-MM-dd}{(c.IsOverdue ? " (OVERDUE)" : "")}";
            var block = c.IsBlocked ? ", BLOCKED (waiting on another task)" : "";
            lines.Append($"{i + 1}. \"{c.Title}\" — priority {c.Priority}, {due}, project {c.ProjectName}{block}\n");
        }

        var system =
            "You are a focus assistant. From the user's open tasks, pick the best order to work on them " +
            $"next, up to {limit}. Prefer tasks that are overdue or due soon and higher priority; put " +
            "BLOCKED tasks last (they can't be started yet). Reply with ONLY lines in the form 'N: reason', " +
            "where N is the task number, most important first, one line per task, no extra text. Keep each " +
            "reason under 100 characters. Reply in the same language as the task titles.";

        var reply = await _llm.CompleteAsync(new List<ChatBotMessage>
        {
            new("system", system),
            new("user", lines.ToString()),
        });
        if (!reply.Succeeded)
        {
            _logger.LogWarning("Next-action ranking failed: {Error}", reply.Error);
            return null;
        }

        var items = ParseRanking(reply.Value!, pool, limit);
        if (items.Count == 0)
            return null;

        return new NextActionsDto { Enabled = true, RankedByAi = true, Items = items };
    }

    /// <summary>
    /// Maps the model's "N: reason" lines back onto the candidate pool, in the order given, then
    /// appends any candidates it left out so the list stays complete up to the limit.
    /// </summary>
    private static List<NextActionItemDto> ParseRanking(string text, List<NextActionItemDto> pool, int limit)
    {
        var result = new List<NextActionItemDto>();
        var used = new HashSet<int>();

        foreach (var rawLine in text.Split('\n'))
        {
            var m = RankLine().Match(rawLine.Trim());
            if (!m.Success) continue;
            var n = int.Parse(m.Groups["n"].Value);
            if (n < 1 || n > pool.Count || !used.Add(n)) continue;

            var item = pool[n - 1];
            item.Reason = m.Groups["reason"].Value.Trim();
            result.Add(item);
            if (result.Count >= limit) break;
        }

        // Backfill anything the model skipped, preserving the deterministic order.
        if (result.Count < limit)
        {
            for (var i = 0; i < pool.Count && result.Count < limit; i++)
                if (used.Add(i + 1))
                    result.Add(pool[i]);
        }

        return result;
    }

    private static int PriorityRank(string priority) => priority switch
    {
        nameof(TaskPriority.High) => 3,
        nameof(TaskPriority.Medium) => 2,
        _ => 1,
    };

    [GeneratedRegex(@"^\s*#?(?<n>\d+)\s*[:.)\-]\s*(?<reason>.+)$")]
    private static partial Regex RankLine();
}
