using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Projects;

namespace Taskpilot.API.Services;

/// <summary>Aggregate delivery metrics for a project board (status/priority mix, weekly trend, cycle time, workload).</summary>
public interface IProjectAnalyticsService
{
    Task<Result<ProjectAnalyticsDto>> GetAnalyticsAsync(Guid userId, Guid projectId);
}
