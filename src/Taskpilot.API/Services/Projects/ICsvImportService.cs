using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Projects;

namespace Taskpilot.API.Services;

/// <summary>Imports tasks into a project from CSV text (the counterpart to CSV export).</summary>
public interface ICsvImportService
{
    /// <summary>Parses the CSV and creates tasks in the project (owner/Editor).</summary>
    Task<Result<ImportResultDto>> ImportTasksAsync(Guid userId, Guid projectId, string csv);
}
