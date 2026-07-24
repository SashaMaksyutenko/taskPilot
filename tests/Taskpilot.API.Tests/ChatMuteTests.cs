using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Chat;
using Taskpilot.API.Hubs;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Tests muting a conversation (spec module 6): the flag is set per participant, surfaced in
/// the conversation list, and silences new-message notifications for the muter.
/// </summary>
public class ChatMuteTests
{
    private readonly Mock<INotificationService> _notifications = new();

    private ChatService Create(TaskpilotDbContext ctx) =>
        new(ctx, _notifications.Object, Mock.Of<IWebhookService>(), new PresenceTracker(),
            NullLogger<ChatService>.Instance);

    /// <summary>Seeds a group conversation with the given members and returns its id.</summary>
    private static async Task<Guid> SeedConversationAsync(TaskpilotDbContext ctx, params Guid[] memberIds)
    {
        var convId = Guid.NewGuid();
        ctx.Conversations.Add(new Conversation { Id = convId, Type = ConversationType.Group, Name = "Team" });
        foreach (var uid in memberIds)
            ctx.ConversationParticipants.Add(new ConversationParticipant
            {
                Id = Guid.NewGuid(), ConversationId = convId, UserId = uid,
            });
        await ctx.SaveChangesAsync();
        return convId;
    }

    [Fact]
    public async Task SetMuted_SetsTheFlag_AndReflectsInTheConversationList()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var other = await TestDb.AddUserAsync(ctx, "Other");
        var convId = await SeedConversationAsync(ctx, me, other);
        var svc = Create(ctx);

        var result = await svc.SetConversationMutedAsync(me, convId, muted: true);

        Assert.True(result.Succeeded);
        Assert.True(result.Value);
        // The conversation list reports it muted for me...
        var mine = (await svc.GetUserConversationsAsync(me)).Value!;
        Assert.True(mine.Single().Muted);
        // ...but not for the other participant (mute is per-user).
        var theirs = (await svc.GetUserConversationsAsync(other)).Value!;
        Assert.False(theirs.Single().Muted);
    }

    [Fact]
    public async Task SetMuted_ByANonParticipant_IsRefused()
    {
        await using var ctx = TestDb.CreateContext();
        var member = await TestDb.AddUserAsync(ctx, "Member");
        var outsider = await TestDb.AddUserAsync(ctx, "Outsider");
        var convId = await SeedConversationAsync(ctx, member);
        var svc = Create(ctx);

        var result = await svc.SetConversationMutedAsync(outsider, convId, muted: true);

        Assert.False(result.Succeeded);
        Assert.Equal("You are not a participant of this conversation.", result.Error);
    }

    [Fact]
    public async Task SendMessage_DoesNotNotifyAMutedParticipant_ButNotifiesAnUnmutedOne()
    {
        await using var ctx = TestDb.CreateContext();
        var sender = await TestDb.AddUserAsync(ctx, "Sender");
        var muted = await TestDb.AddUserAsync(ctx, "Muted");
        var awake = await TestDb.AddUserAsync(ctx, "Awake");
        var convId = await SeedConversationAsync(ctx, sender, muted, awake);
        var svc = Create(ctx);
        // "Muted" has muted the conversation; both recipients are offline (no presence set).
        await svc.SetConversationMutedAsync(muted, convId, muted: true);

        var result = await svc.SendMessageAsync(sender, new SendMessageDto { ConversationId = convId, Content = "hi team" });

        Assert.True(result.Succeeded);
        // The muted participant gets no notification; the unmuted (offline) one does.
        _notifications.Verify(n => n.CreateAsync(muted, It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
        _notifications.Verify(n => n.CreateAsync(awake, NotificationType.Chat, It.IsAny<string>(), It.IsAny<string?>()), Times.Once);
    }
}
