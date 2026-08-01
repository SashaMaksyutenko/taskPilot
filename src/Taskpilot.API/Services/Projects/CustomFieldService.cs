using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Projects;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <inheritdoc />
public class CustomFieldService : ICustomFieldService
{
    private readonly TaskpilotDbContext _context;
    private readonly ILogger<CustomFieldService> _logger;

    public CustomFieldService(TaskpilotDbContext context, ILogger<CustomFieldService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<List<CustomFieldDefinitionDto>>> GetDefinitionsAsync(Guid userId, Guid projectId)
    {
        if (!await ProjectAccess.CanAccessAsync(_context, projectId, userId))
            return Result<List<CustomFieldDefinitionDto>>.Fail("Project not found.");

        return Result<List<CustomFieldDefinitionDto>>.Ok(await LoadDefinitionsAsync(projectId));
    }

    /// <inheritdoc />
    public async Task<Result<CustomFieldDefinitionDto>> CreateDefinitionAsync(Guid userId, Guid projectId, CreateCustomFieldDto dto)
    {
        if (!await ProjectAccess.CanWriteAsync(_context, projectId, userId))
            return Result<CustomFieldDefinitionDto>.Fail("You have read-only access to this project.");

        var name = (dto.Name ?? string.Empty).Trim();
        if (name.Length is < 1 or > 80)
            return Result<CustomFieldDefinitionDto>.Fail("A field name (1–80 chars) is required.");
        if (!Enum.TryParse<CustomFieldType>(dto.Type, ignoreCase: true, out var type))
            return Result<CustomFieldDefinitionDto>.Fail("Invalid field type.");

        var options = ParseOptions(dto.Options);
        if (type == CustomFieldType.Select && options.Count == 0)
            return Result<CustomFieldDefinitionDto>.Fail("A Select field needs at least one option.");

        var nextPosition = await _context.CustomFieldDefinitions
            .Where(f => f.ProjectId == projectId)
            .Select(f => (int?)f.Position)
            .MaxAsync() ?? -1;

        var field = new CustomFieldDefinition
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = name,
            Type = type,
            Options = type == CustomFieldType.Select ? string.Join('\n', options) : null,
            Position = nextPosition + 1,
            CreatedAt = DateTime.UtcNow,
        };
        _context.CustomFieldDefinitions.Add(field);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Custom field created. Project: {Project}, Field: {Field}", projectId, field.Id);
        return Result<CustomFieldDefinitionDto>.Ok(MapDefinition(field));
    }

    /// <inheritdoc />
    public async Task<Result> DeleteDefinitionAsync(Guid userId, Guid fieldId)
    {
        var field = await _context.CustomFieldDefinitions.FirstOrDefaultAsync(f => f.Id == fieldId);
        if (field is null)
            return Result.Fail("Field not found.");
        if (!await ProjectAccess.CanWriteAsync(_context, field.ProjectId, userId))
            return Result.Fail("You have read-only access to this project.");

        // The value→field FK is restricted, so clear the field's values before removing it.
        var values = _context.CustomFieldValues.Where(v => v.FieldId == fieldId);
        _context.CustomFieldValues.RemoveRange(values);
        _context.CustomFieldDefinitions.Remove(field);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Custom field deleted. Field: {Field}", fieldId);
        return Result.Ok();
    }

    /// <inheritdoc />
    public async Task<Result<List<TaskFieldDto>>> GetTaskFieldsAsync(Guid userId, Guid taskId)
    {
        var projectId = await ProjectIdOfTaskAsync(taskId);
        if (projectId is null || !await ProjectAccess.CanAccessAsync(_context, projectId.Value, userId))
            return Result<List<TaskFieldDto>>.Fail("Task not found.");

        return Result<List<TaskFieldDto>>.Ok(await BuildTaskFieldsAsync(projectId.Value, taskId));
    }

    /// <inheritdoc />
    public async Task<Result<List<TaskFieldDto>>> SetTaskValueAsync(Guid userId, Guid taskId, Guid fieldId, string value)
    {
        var projectId = await ProjectIdOfTaskAsync(taskId);
        if (projectId is null)
            return Result<List<TaskFieldDto>>.Fail("Task not found.");
        if (!await ProjectAccess.CanWriteAsync(_context, projectId.Value, userId))
            return Result<List<TaskFieldDto>>.Fail("You have read-only access to this project.");

        var field = await _context.CustomFieldDefinitions
            .FirstOrDefaultAsync(f => f.Id == fieldId && f.ProjectId == projectId);
        if (field is null)
            return Result<List<TaskFieldDto>>.Fail("Field not found.");

        value = (value ?? string.Empty).Trim();
        if (value.Length > 2000)
            return Result<List<TaskFieldDto>>.Fail("Value is too long.");
        if (value.Length > 0 && !IsValidForType(field, value))
            return Result<List<TaskFieldDto>>.Fail($"Value is not valid for a {field.Type} field.");

        var existing = await _context.CustomFieldValues
            .FirstOrDefaultAsync(v => v.TaskId == taskId && v.FieldId == fieldId);

        // An empty value clears the field; otherwise upsert.
        if (value.Length == 0)
        {
            if (existing is not null)
                _context.CustomFieldValues.Remove(existing);
        }
        else if (existing is null)
        {
            _context.CustomFieldValues.Add(new CustomFieldValue
            {
                Id = Guid.NewGuid(), TaskId = taskId, FieldId = fieldId, Value = value, UpdatedAt = DateTime.UtcNow,
            });
        }
        else
        {
            existing.Value = value;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync();

        return Result<List<TaskFieldDto>>.Ok(await BuildTaskFieldsAsync(projectId.Value, taskId));
    }

    /// <summary>Merges the project's field definitions with this task's stored values.</summary>
    private async Task<List<TaskFieldDto>> BuildTaskFieldsAsync(Guid projectId, Guid taskId)
    {
        var fields = await _context.CustomFieldDefinitions
            .Where(f => f.ProjectId == projectId)
            .OrderBy(f => f.Position)
            .Select(f => new { f.Id, f.Name, f.Type, f.Options })
            .AsNoTracking()
            .ToListAsync();

        var values = await _context.CustomFieldValues
            .Where(v => v.TaskId == taskId)
            .Select(v => new { v.FieldId, v.Value })
            .AsNoTracking()
            .ToListAsync();
        var byField = values.ToDictionary(v => v.FieldId, v => v.Value);

        return fields.Select(f => new TaskFieldDto
        {
            FieldId = f.Id,
            Name = f.Name,
            Type = f.Type.ToString(),
            Options = f.Type == CustomFieldType.Select ? ParseOptions(f.Options) : new List<string>(),
            Value = byField.TryGetValue(f.Id, out var v) ? v : string.Empty,
        }).ToList();
    }

    private async Task<List<CustomFieldDefinitionDto>> LoadDefinitionsAsync(Guid projectId)
    {
        var fields = await _context.CustomFieldDefinitions
            .Where(f => f.ProjectId == projectId)
            .OrderBy(f => f.Position)
            .AsNoTracking()
            .ToListAsync();
        return fields.Select(MapDefinition).ToList();
    }

    private static CustomFieldDefinitionDto MapDefinition(CustomFieldDefinition f) => new()
    {
        Id = f.Id,
        Name = f.Name,
        Type = f.Type.ToString(),
        Options = f.Type == CustomFieldType.Select ? ParseOptions(f.Options) : new List<string>(),
        Position = f.Position,
    };

    /// <summary>Whether a non-empty value is acceptable for the field's type.</summary>
    private static bool IsValidForType(CustomFieldDefinition field, string value) => field.Type switch
    {
        CustomFieldType.Number => double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out _),
        CustomFieldType.Date => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
        CustomFieldType.Select => ParseOptions(field.Options).Contains(value),
        _ => true, // Text accepts anything.
    };

    /// <summary>Splits the stored newline-separated options into a trimmed, non-empty list.</summary>
    private static List<string> ParseOptions(string? options) =>
        (options ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

    private Task<Guid?> ProjectIdOfTaskAsync(Guid taskId) =>
        _context.ProjectTasks.Where(t => t.Id == taskId).Select(t => (Guid?)t.ProjectId).FirstOrDefaultAsync();
}
