using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Data;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Shared project access rules. A user may access a project when they own it or are
/// a member; the owner alone manages members and archives/deletes the project.
/// </summary>
public static class ProjectAccess
{
    /// <summary>Predicate: the project is owned by, or shared with, the user.</summary>
    public static Expression<Func<Project, bool>> AccessibleBy(Guid userId) =>
        p => p.OwnerId == userId || p.Members.Any(m => m.UserId == userId);

    /// <summary>True if the user owns the project or is a member of it.</summary>
    public static Task<bool> CanAccessAsync(TaskpilotDbContext db, Guid projectId, Guid userId) =>
        db.Projects.AnyAsync(p => p.Id == projectId &&
            (p.OwnerId == userId || p.Members.Any(m => m.UserId == userId)));

    /// <summary>True if the user may write to the project (owner or an Editor member; Viewers are read-only).</summary>
    public static Task<bool> CanWriteAsync(TaskpilotDbContext db, Guid projectId, Guid userId) =>
        db.Projects.AnyAsync(p => p.Id == projectId &&
            (p.OwnerId == userId || p.Members.Any(m => m.UserId == userId && m.Role == ProjectMemberRole.Editor)));

    /// <summary>True if the user may write to the project that owns the given task.</summary>
    public static Task<bool> CanWriteTaskAsync(TaskpilotDbContext db, Guid taskId, Guid userId) =>
        db.ProjectTasks.AnyAsync(t => t.Id == taskId &&
            (t.Project.OwnerId == userId || t.Project.Members.Any(m => m.UserId == userId && m.Role == ProjectMemberRole.Editor)));
}
