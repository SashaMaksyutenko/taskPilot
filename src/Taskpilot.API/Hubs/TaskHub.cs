using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Data;

namespace Taskpilot.API.Hubs;

/// <summary>
/// SignalR hub for real-time task comments. Clients viewing a task join its group to
/// receive new/removed comments instantly. Posting is done over REST
/// (TaskCommentsController); this hub only manages per-task groups.
///
/// Receive events: "ReceiveComment" (TaskCommentDto) and "RemoveComment" (Guid id).
/// </summary>
[Authorize]
public class TaskHub : Hub
{
    private readonly TaskpilotDbContext _context;

    public TaskHub(TaskpilotDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Subscribes the current connection to a task's comment stream, but only if the
    /// caller owns or collaborates on the task's project.
    /// </summary>
    public async Task JoinTask(Guid taskId)
    {
        var userId = GetUserId();
        if (userId is null)
            return;

        var canAccess = await _context.ProjectTasks.AnyAsync(t => t.Id == taskId &&
            (t.Project.OwnerId == userId || t.Project.Members.Any(m => m.UserId == userId)));
        if (!canAccess)
            return;

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(taskId));
    }

    /// <summary>Unsubscribes the current connection from a task.</summary>
    public Task LeaveTask(Guid taskId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(taskId));

    /// <summary>SignalR group name for a task's comment stream.</summary>
    public static string GroupName(Guid taskId) => $"task-{taskId}";

    /// <summary>Current user's id from the JWT "sub" claim, or null.</summary>
    private Guid? GetUserId()
    {
        var sub = Context.User?.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
