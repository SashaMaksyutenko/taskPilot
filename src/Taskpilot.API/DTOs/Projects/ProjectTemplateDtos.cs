namespace Taskpilot.API.DTOs.Projects;

/// <summary>A project template in list form.</summary>
public class ProjectTemplateDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Color { get; set; }

    /// <summary>How many tasks the template will stamp out.</summary>
    public int TaskCount { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>A project template with its tasks, for previewing before use.</summary>
public class ProjectTemplateDetailDto : ProjectTemplateDto
{
    public List<TemplateTaskDto> Tasks { get; set; } = new();
}

/// <summary>One task inside a template.</summary>
public class TemplateTaskDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Priority { get; set; } = string.Empty;

    /// <summary>Deadline as days from the project's start; null if the task has none.</summary>
    public int? DeadlineOffsetDays { get; set; }

    /// <summary>Parent template task id for subtasks; null for a top-level task.</summary>
    public Guid? ParentTemplateTaskId { get; set; }

    public List<string> Tags { get; set; } = new();
}

/// <summary>Input for saving a project as a template.</summary>
public class SaveAsTemplateDto
{
    /// <summary>Optional name; defaults to the source project's name when omitted.</summary>
    public string? Name { get; set; }
}

/// <summary>Input for creating a project from a template.</summary>
public class CreateFromTemplateDto
{
    /// <summary>Optional name; defaults to the template's name when omitted.</summary>
    public string? Name { get; set; }

    /// <summary>Optional color override; falls back to the template's color.</summary>
    public string? Color { get; set; }
}
