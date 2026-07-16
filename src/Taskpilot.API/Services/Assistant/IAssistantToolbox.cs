namespace Taskpilot.API.Services.Assistant;

/// <summary>
/// The read-only tools the assistant may call to look up the signed-in user's data
/// (their tasks, deadlines and projects). Every call is scoped to that user.
/// </summary>
public interface IAssistantToolbox
{
    /// <summary>The tool definitions advertised to the model.</summary>
    IReadOnlyList<ToolDefinition> Definitions { get; }

    /// <summary>Runs a tool by name with its JSON arguments; returns a compact JSON result.</summary>
    Task<string> ExecuteAsync(Guid userId, string toolName, string argumentsJson);
}
