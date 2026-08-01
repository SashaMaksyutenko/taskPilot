using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Projects;

namespace Taskpilot.API.Services;

/// <summary>Manages project custom-field definitions and per-task values.</summary>
public interface ICustomFieldService
{
    /// <summary>Lists a project's custom-field definitions in order (any member).</summary>
    Task<Result<List<CustomFieldDefinitionDto>>> GetDefinitionsAsync(Guid userId, Guid projectId);

    /// <summary>Adds a custom-field definition to a project (owner/Editor).</summary>
    Task<Result<CustomFieldDefinitionDto>> CreateDefinitionAsync(Guid userId, Guid projectId, CreateCustomFieldDto dto);

    /// <summary>Removes a definition and every task value for it (owner/Editor).</summary>
    Task<Result> DeleteDefinitionAsync(Guid userId, Guid fieldId);

    /// <summary>A task's custom fields merged with its current values (any member).</summary>
    Task<Result<List<TaskFieldDto>>> GetTaskFieldsAsync(Guid userId, Guid taskId);

    /// <summary>Sets or clears a task's value for a field (owner/Editor); returns the task's fields.</summary>
    Task<Result<List<TaskFieldDto>>> SetTaskValueAsync(Guid userId, Guid taskId, Guid fieldId, string value);
}
