using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Data;
using Taskpilot.API.Models;

namespace Taskpilot.API.Tests;

/// <summary>
/// Helpers for building an isolated in-memory database context and seeding test data.
/// </summary>
public static class TestDb
{
    /// <summary>Creates a fresh in-memory context (unique database per call).</summary>
    public static TaskpilotDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TaskpilotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TaskpilotDbContext(options);
    }

    /// <summary>Adds a user and returns its id.</summary>
    public static async Task<Guid> AddUserAsync(TaskpilotDbContext ctx, string name = "User")
    {
        var id = Guid.NewGuid();
        ctx.Users.Add(new User
        {
            Id = id,
            Name = name,
            Email = $"{id:N}@test.local",
            PasswordHash = "hash",
            Role = Role.Developer,
            IsActive = true,
        });
        await ctx.SaveChangesAsync();
        return id;
    }

    /// <summary>Adds a project owned by the given user and returns its id.</summary>
    public static async Task<Guid> AddProjectAsync(TaskpilotDbContext ctx, Guid ownerId, string name = "Project")
    {
        var id = Guid.NewGuid();
        ctx.Projects.Add(new Project { Id = id, Name = name, OwnerId = ownerId });
        await ctx.SaveChangesAsync();
        return id;
    }
}
