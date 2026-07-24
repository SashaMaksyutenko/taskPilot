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
/// Tests blocking a user in direct messaging (spec module 3): while a block exists either
/// way, neither side can send a DM or open a direct conversation; group chats are unaffected;
/// the block is symmetric, idempotent and surfaced on the conversation list.
/// </summary>
public class ChatBlockTests
{
    private readonly Mock<INotificationService> _notifications = new();

    private ChatService Create(TaskpilotDbContext ctx) =>
        new(ctx, _notifications.Object, Mock.Of<IWebhookService>(), new PresenceTracker(),
            NullLogger<ChatService>.Instance);

    private static async Task<Guid> SeedGroupAsync(TaskpilotDbContext ctx, params Guid[] memberIds)
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
    public async Task Block_StopsDirectMessagesBothWays()
    {
        await using var ctx = TestDb.CreateContext();
        var a = await TestDb.AddUserAsync(ctx, "A");
        var b = await TestDb.AddUserAsync(ctx, "B");
        var svc = Create(ctx);
        var conv = (await svc.StartDirectConversationAsync(a, b)).Value!;

        await svc.BlockUserAsync(a, b); // A blocks B

        // The blocked user cannot message the blocker...
        var asB = await svc.SendMessageAsync(b, new SendMessageDto { ConversationId = conv.Id, Content = "hi" });
        Assert.False(asB.Succeeded);
        Assert.Equal("You can no longer message this user.", asB.Error);
        // ...and the blocker cannot message the blocked user either (symmetric).
        var asA = await svc.SendMessageAsync(a, new SendMessageDto { ConversationId = conv.Id, Content = "hi" });
        Assert.False(asA.Succeeded);
    }

    [Fact]
    public async Task Block_PreventsOpeningANewDirectConversation()
    {
        await using var ctx = TestDb.CreateContext();
        var a = await TestDb.AddUserAsync(ctx, "A");
        var b = await TestDb.AddUserAsync(ctx, "B");
        var svc = Create(ctx);

        await svc.BlockUserAsync(a, b);

        var result = await svc.StartDirectConversationAsync(b, a);
        Assert.False(result.Succeeded);
        Assert.Equal("You cannot start a conversation with this user.", result.Error);
    }

    [Fact]
    public async Task Unblock_RestoresMessaging()
    {
        await using var ctx = TestDb.CreateContext();
        var a = await TestDb.AddUserAsync(ctx, "A");
        var b = await TestDb.AddUserAsync(ctx, "B");
        var svc = Create(ctx);
        var conv = (await svc.StartDirectConversationAsync(a, b)).Value!;
        await svc.BlockUserAsync(a, b);

        await svc.UnblockUserAsync(a, b);

        var result = await svc.SendMessageAsync(b, new SendMessageDto { ConversationId = conv.Id, Content = "hi again" });
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task ConversationList_FlagsBlockedForTheBlockerOnly_AndBlockedListIsReturned()
    {
        await using var ctx = TestDb.CreateContext();
        var a = await TestDb.AddUserAsync(ctx, "A");
        var b = await TestDb.AddUserAsync(ctx, "B");
        var svc = Create(ctx);
        await svc.StartDirectConversationAsync(a, b);

        await svc.BlockUserAsync(a, b);

        Assert.True((await svc.GetUserConversationsAsync(a)).Value!.Single().Blocked);
        Assert.False((await svc.GetUserConversationsAsync(b)).Value!.Single().Blocked);
        var blocked = (await svc.GetBlockedUsersAsync(a)).Value!;
        Assert.Equal(b, blocked.Single().UserId);
    }

    [Fact]
    public async Task Block_IsIdempotent_AndSelfBlockIsRefused()
    {
        await using var ctx = TestDb.CreateContext();
        var a = await TestDb.AddUserAsync(ctx, "A");
        var b = await TestDb.AddUserAsync(ctx, "B");
        var svc = Create(ctx);

        var self = await svc.BlockUserAsync(a, a);
        Assert.False(self.Succeeded);
        Assert.Equal("You cannot block yourself.", self.Error);

        await svc.BlockUserAsync(a, b);
        await svc.BlockUserAsync(a, b); // no duplicate row, no error
        Assert.Equal(1, await ctx.UserBlocks.CountAsync());
    }

    [Fact]
    public async Task Block_DoesNotAffectGroupChats()
    {
        await using var ctx = TestDb.CreateContext();
        var a = await TestDb.AddUserAsync(ctx, "A");
        var b = await TestDb.AddUserAsync(ctx, "B");
        var convId = await SeedGroupAsync(ctx, a, b);
        var svc = Create(ctx);
        await svc.BlockUserAsync(a, b);

        // Blocking is a 1:1 concept; a blocked member can still post in a shared group.
        var result = await svc.SendMessageAsync(b, new SendMessageDto { ConversationId = convId, Content = "group hi" });
        Assert.True(result.Succeeded);
    }
}
