using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Taskpilot.API.DTOs.Users;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Tests for profile skill tags (spec module 10): they are normalized on save, shown on the
/// public profile, and scrubbed when the account is deleted.
/// </summary>
public class UserSkillsTests
{
    private static UserService Create(Taskpilot.API.Data.TaskpilotDbContext ctx) =>
        new(ctx, Mock.Of<IFileService>(), NullLogger<UserService>.Instance);

    private static UpdateProfileDto Profile(params string[] skills) =>
        new() { Name = "Dev", Skills = skills.ToList() };

    [Fact]
    public async Task UpdateProfile_NormalizesSkills_TrimDedupeAndDropBlanks()
    {
        await using var ctx = TestDb.CreateContext();
        var id = await TestDb.AddUserAsync(ctx, "Dev");
        var svc = Create(ctx);

        // Whitespace, a blank entry, and a case-different duplicate of "React".
        var result = await svc.UpdateProfileAsync(id, Profile("  C#  ", "React", "", "react"));

        Assert.True(result.Succeeded);
        Assert.Equal(new[] { "C#", "React" }, result.Value!.Skills.ToArray());
        // Persisted too.
        var saved = await ctx.Users.AsNoTracking().FirstAsync(u => u.Id == id);
        Assert.Equal(new[] { "C#", "React" }, saved.Skills.ToArray());
    }

    [Fact]
    public async Task UpdateProfile_CapsTheNumberOfSkills()
    {
        await using var ctx = TestDb.CreateContext();
        var id = await TestDb.AddUserAsync(ctx, "Dev");
        var svc = Create(ctx);

        // 40 distinct skills — the list is capped at 30.
        var many = Enumerable.Range(1, 40).Select(i => $"skill{i}").ToArray();
        var result = await svc.UpdateProfileAsync(id, Profile(many));

        Assert.Equal(30, result.Value!.Skills.Count);
    }

    [Fact]
    public async Task PublicProfile_IncludesSkills()
    {
        await using var ctx = TestDb.CreateContext();
        var id = await TestDb.AddUserAsync(ctx, "Dev");
        var svc = Create(ctx);
        await svc.UpdateProfileAsync(id, Profile("Go", "Docker"));

        var profile = await svc.GetPublicProfileAsync(id);

        Assert.True(profile.Succeeded);
        Assert.Equal(new[] { "Go", "Docker" }, profile.Value!.Skills.ToArray());
    }

    [Fact]
    public async Task DeleteAccount_ScrubsSkills()
    {
        await using var ctx = TestDb.CreateContext();
        var id = Guid.NewGuid();
        ctx.Users.Add(new User
        {
            Id = id, Name = "Dev", Email = "dev@test.local", Role = Role.Developer, IsActive = true,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Passw0rd!23"),
            Skills = ["C#", "React"],
        });
        await ctx.SaveChangesAsync();
        var svc = Create(ctx);

        var result = await svc.DeleteAccountAsync(id, "Passw0rd!23");

        Assert.True(result.Succeeded);
        var scrubbed = await ctx.Users.AsNoTracking().FirstAsync(u => u.Id == id);
        Assert.Empty(scrubbed.Skills);
    }
}
