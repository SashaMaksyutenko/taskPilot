using System.Text.Json;

namespace Taskpilot.API.Services.Assistant;

/// <summary>
/// Presents the read-only toolbox and the write/action toolbox to the agent as a single
/// set of tools, routing each call to whichever toolbox declares it.
/// </summary>
public class CompositeAssistantToolbox : IAssistantToolbox
{
    private readonly IReadOnlyList<IAssistantToolbox> _toolboxes;

    public CompositeAssistantToolbox(AssistantToolbox read, AssistantActionsToolbox actions)
    {
        _toolboxes = new IAssistantToolbox[] { read, actions };
        Definitions = _toolboxes.SelectMany(t => t.Definitions).ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<ToolDefinition> Definitions { get; }

    /// <inheritdoc />
    public Task<string> ExecuteAsync(Guid userId, string toolName, string argumentsJson)
    {
        foreach (var toolbox in _toolboxes)
        {
            if (toolbox.Definitions.Any(d => d.Name == toolName))
                return toolbox.ExecuteAsync(userId, toolName, argumentsJson);
        }

        return Task.FromResult(JsonSerializer.Serialize(new { error = $"Unknown tool: {toolName}" }));
    }
}
