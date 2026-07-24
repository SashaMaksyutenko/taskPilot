using System.Text.Json;
using Moq;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Users;
using Taskpilot.API.Services;
using Taskpilot.API.Services.Assistant;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Unit tests for the people toolbox. The user service is mocked; the tests check name
/// resolution and that only the vetted public-profile surface is returned.
/// </summary>
public class AssistantPeopleToolboxTests
{
    [Fact]
    public async Task GetUserProfile_ResolvesByName_AndReturnsPublicProfile()
    {
        await using var ctx = TestDb.CreateContext();
        var jane = await TestDb.AddUserAsync(ctx, "Jane Doe");

        var users = new Mock<IUserService>();
        users.Setup(u => u.GetPublicProfileAsync(jane, It.IsAny<Guid?>()))
            .ReturnsAsync(Result<PublicProfileDto>.Ok(new PublicProfileDto
            {
                Id = jane, Name = "Jane Doe", Role = "Developer", Title = "Backend engineer",
                ReputationPoints = 42, Email = null, // not shown → stays null
            }));

        var box = new AssistantPeopleToolbox(ctx, users.Object);
        var json = await box.ExecuteAsync(Guid.NewGuid(), "get_user_profile", "{\"name\":\"jane\"}");

        using var doc = JsonDocument.Parse(json);
        Assert.Equal("Jane Doe", doc.RootElement.GetProperty("name").GetString());
        Assert.Equal("Backend engineer", doc.RootElement.GetProperty("title").GetString());
        Assert.Equal(42, doc.RootElement.GetProperty("reputationPoints").GetInt32());
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("email").ValueKind); // private unless opted in
        users.Verify(u => u.GetPublicProfileAsync(jane, It.IsAny<Guid?>()), Times.Once);
    }

    [Fact]
    public async Task GetUserProfile_UnknownName_DoesNotHitTheService()
    {
        await using var ctx = TestDb.CreateContext();
        await TestDb.AddUserAsync(ctx, "Jane Doe");
        var users = new Mock<IUserService>();

        var box = new AssistantPeopleToolbox(ctx, users.Object);
        var json = await box.ExecuteAsync(Guid.NewGuid(), "get_user_profile", "{\"name\":\"nobody\"}");

        Assert.True(JsonDocument.Parse(json).RootElement.TryGetProperty("error", out _));
        users.Verify(u => u.GetPublicProfileAsync(It.IsAny<Guid>(), It.IsAny<Guid?>()), Times.Never);
    }
}
