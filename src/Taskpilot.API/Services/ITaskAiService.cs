using Taskpilot.API.Common;

namespace Taskpilot.API.Services;

/// <summary>AI helpers scoped to a single task (config-gated on the LLM client).</summary>
public interface ITaskAiService
{
    /// <summary>True only when the LLM client has an API key configured.</summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Suggests a short checklist of concrete subtasks for a task the user can access.
    /// Returns the parsed suggestions (the user chooses which to actually create).
    /// </summary>
    Task<Result<List<string>>> SuggestSubtasksAsync(Guid userId, Guid taskId);
}
