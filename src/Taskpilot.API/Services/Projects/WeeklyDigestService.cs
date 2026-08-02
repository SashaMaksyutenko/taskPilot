using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Digest;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <inheritdoc />
public class WeeklyDigestService : IWeeklyDigestService
{
    private readonly TaskpilotDbContext _context;
    private readonly IChatBotClient _llm;
    private readonly ILogger<WeeklyDigestService> _logger;

    public WeeklyDigestService(TaskpilotDbContext context, IChatBotClient llm, ILogger<WeeklyDigestService> logger)
    {
        _context = context;
        _llm = llm;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsEnabled => _llm.IsEnabled;

    /// <inheritdoc />
    public async Task<DigestDto> GetWeeklyAsync(Guid userId)
    {
        var now = DateTime.UtcNow;
        var weekAgo = now.AddDays(-7);
        var weekAhead = now.AddDays(7);

        // Tasks in every project the user owns or collaborates on.
        var tasks = _context.ProjectTasks
            .Where(t => t.Project.OwnerId == userId || t.Project.Members.Any(m => m.UserId == userId));

        var completedQ = tasks.Where(t => t.CompletedAt != null && t.CompletedAt >= weekAgo);
        var overdueQ = tasks.Where(t => t.Deadline != null && t.Deadline < now && t.Status != ProjectTaskStatus.Done);
        var dueSoonQ = tasks.Where(t => t.Deadline != null && t.Deadline >= now && t.Deadline <= weekAhead && t.Status != ProjectTaskStatus.Done);

        return new DigestDto
        {
            WeekStart = weekAgo,
            Completed = await completedQ.CountAsync(),
            Created = await tasks.CountAsync(t => t.CreatedAt >= weekAgo),
            Overdue = await overdueQ.CountAsync(),
            DueSoon = await dueSoonQ.CountAsync(),
            TopCompleted = await completedQ.OrderByDescending(t => t.CompletedAt).Take(5).Select(t => t.Title).ToListAsync(),
            TopOverdue = await overdueQ.OrderBy(t => t.Deadline).Take(5).Select(t => t.Title).ToListAsync(),
            TopDueSoon = await dueSoonQ.OrderBy(t => t.Deadline).Take(5).Select(t => t.Title).ToListAsync(),
        };
    }

    /// <inheritdoc />
    public async Task<Result<DigestSummaryDto>> GetSummaryAsync(Guid userId)
    {
        if (!_llm.IsEnabled)
            return Result<DigestSummaryDto>.Ok(new DigestSummaryDto { Enabled = false });

        var digest = await GetWeeklyAsync(userId);

        var facts =
            $"Completed this week: {digest.Completed}. Created this week: {digest.Created}. " +
            $"Overdue now: {digest.Overdue}. Due within a week: {digest.DueSoon}. " +
            $"Recently completed: {Join(digest.TopCompleted)}. " +
            $"Overdue tasks: {Join(digest.TopOverdue)}. Upcoming: {Join(digest.TopDueSoon)}.";

        var messages = new List<ChatBotMessage>
        {
            new("system",
                "You are a concise, encouraging project assistant. In 2–3 short sentences of plain text " +
                "(no markdown, no lists), summarize the user's past week from the numbers given and point out " +
                "what needs attention. If everything is zero, gently encourage them to get started."),
            new("user", facts),
        };

        var reply = await _llm.CompleteAsync(messages);
        if (!reply.Succeeded)
        {
            _logger.LogWarning("Digest summary generation failed: {Error}", reply.Error);
            return Result<DigestSummaryDto>.Fail("Could not generate a summary right now.");
        }

        return Result<DigestSummaryDto>.Ok(new DigestSummaryDto { Enabled = true, Summary = reply.Value!.Trim() });
    }

    private static string Join(List<string> titles) =>
        titles.Count == 0 ? "none" : string.Join("; ", titles);
}
