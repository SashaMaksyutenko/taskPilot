using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
using Taskpilot.API.Data;

namespace Taskpilot.API.Services;

/// <summary>
/// Task-scoped AI helpers. Currently: propose subtasks for a task by asking the LLM for
/// a plain checklist and parsing it into individual titles. A config-gated no-op when no
/// API key is set, exactly like the assistant.
/// </summary>
public partial class TaskAiService : ITaskAiService
{
    private const int MaxSuggestions = 8;
    private const int MaxTitleLength = 120;

    private const string SystemPrompt =
        "You break a project task into a short checklist of concrete, actionable subtasks. " +
        "Reply with ONLY a plain list — one subtask per line, no numbering, no bullets, no " +
        "headings, no commentary. Give 3 to 7 subtasks. Keep each under 80 characters. " +
        "Reply in the same language as the task.";

    private readonly TaskpilotDbContext _context;
    private readonly IChatBotClient _client;
    private readonly ILogger<TaskAiService> _logger;

    public TaskAiService(TaskpilotDbContext context, IChatBotClient client, ILogger<TaskAiService> logger)
    {
        _context = context;
        _client = client;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsEnabled => _client.IsEnabled;

    /// <inheritdoc />
    public async Task<Result<List<string>>> SuggestSubtasksAsync(Guid userId, Guid taskId)
    {
        if (!_client.IsEnabled)
            return Result<List<string>>.Fail("The AI assistant is not configured.");

        var task = await _context.ProjectTasks
            .Where(t => t.Id == taskId)
            .Select(t => new { t.ProjectId, t.Title, t.Description })
            .FirstOrDefaultAsync();
        if (task is null)
            return Result<List<string>>.Fail("Task not found.");

        // Anyone with access to the task's project can use this.
        if (!await ProjectAccess.CanAccessAsync(_context, task.ProjectId, userId))
            return Result<List<string>>.Fail("Task not found.");

        var userPrompt =
            $"Task title: {task.Title}\n" +
            $"Description: {(string.IsNullOrWhiteSpace(task.Description) ? "(none)" : task.Description)}";

        var reply = await _client.CompleteAsync(new List<ChatBotMessage>
        {
            new("system", SystemPrompt),
            new("user", userPrompt),
        });
        if (!reply.Succeeded)
            return Result<List<string>>.Fail(reply.Error!);

        var suggestions = ParseList(reply.Value!);
        if (suggestions.Count == 0)
            return Result<List<string>>.Fail("The assistant did not return any subtasks.");

        _logger.LogInformation("AI suggested {Count} subtasks. TaskId: {TaskId}", suggestions.Count, taskId);
        return Result<List<string>>.Ok(suggestions);
    }

    /// <summary>Turns the model's free-text list into clean, deduplicated subtask titles.</summary>
    private static List<string> ParseList(string text)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();

        foreach (var rawLine in text.Split('\n'))
        {
            // Strip a leading bullet or "1." / "1)" numbering the model may add anyway.
            var line = LeadingBullet().Replace(rawLine.Trim(), string.Empty).Trim();
            if (line.Length == 0)
                continue;
            if (line.Length > MaxTitleLength)
                line = line[..MaxTitleLength].TrimEnd();

            if (seen.Add(line))
                result.Add(line);
            if (result.Count >= MaxSuggestions)
                break;
        }

        return result;
    }

    [GeneratedRegex(@"^[\s\-\*••·–—]*(?:\d+[.)]\s*)?")]
    private static partial Regex LeadingBullet();
}
