using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Projects;

namespace Taskpilot.API.Services;

/// <summary>Handles task deadline-extension requests and the owner's decisions.</summary>
public interface IExtensionService
{
    /// <summary>Raises a pending extension request for a task (one at a time).</summary>
    Task<Result<ExtensionRequestDto>> RequestAsync(Guid userId, Guid taskId, CreateExtensionRequestDto dto);

    /// <summary>
    /// Approves or rejects a pending request (project owner only). Approving moves the
    /// task deadline to the requested date and clears its overdue/escalation flags.
    /// </summary>
    Task<Result<ExtensionRequestDto>> DecideAsync(Guid userId, Guid requestId, bool approve);

    /// <summary>Lists a task's extension requests (newest first) for a user with access.</summary>
    Task<Result<List<ExtensionRequestDto>>> GetForTaskAsync(Guid userId, Guid taskId);
}
