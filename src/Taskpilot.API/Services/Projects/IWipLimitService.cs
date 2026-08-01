using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Projects;

namespace Taskpilot.API.Services;

/// <summary>Manages per-column work-in-progress (WIP) limits on a project's Kanban board.</summary>
public interface IWipLimitService
{
    /// <summary>Lists a project's WIP limits (any project member).</summary>
    Task<Result<List<WipLimitDto>>> GetAsync(Guid userId, Guid projectId);

    /// <summary>Sets or clears a column's WIP limit (owner/Editor); returns the updated list.</summary>
    Task<Result<List<WipLimitDto>>> SetAsync(Guid userId, Guid projectId, SetWipLimitDto dto);
}
