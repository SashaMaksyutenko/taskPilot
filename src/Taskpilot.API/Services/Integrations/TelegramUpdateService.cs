using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.ChatBot;
using Taskpilot.API.DTOs.Integrations;
using Taskpilot.API.Models;
using Taskpilot.API.Services.Assistant;
using Taskpilot.Integrations;

namespace Taskpilot.API.Services;

/// <inheritdoc />
public class TelegramUpdateService : ITelegramUpdateService
{
    private readonly TaskpilotDbContext _context;
    private readonly ITelegramLinkService _link;
    private readonly ITelegramSender _sender;
    private readonly IAssistantAgent _assistant;
    private readonly ILogger<TelegramUpdateService> _logger;

    private const string HelpText =
        "TaskPilot bot commands:\n" +
        "/start <code> — link your account (get the code in TaskPilot → Settings → Telegram)\n" +
        "/unlink — disconnect this chat\n" +
        "/help — show this help\n\n" +
        "Once linked, just message me in plain language — e.g. \"what tasks are overdue?\" or " +
        "\"create a task 'Write docs' in Website\" — and I'll do it.";

    public TelegramUpdateService(
        TaskpilotDbContext context, ITelegramLinkService link, ITelegramSender sender,
        IAssistantAgent assistant, ILogger<TelegramUpdateService> logger)
    {
        _context = context;
        _link = link;
        _sender = sender;
        _assistant = assistant;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task HandleUpdateAsync(TelegramUpdate update)
    {
        var msg = update.Message;
        if (msg?.Chat is null || string.IsNullOrWhiteSpace(msg.Text))
            return; // only text messages are handled

        var chatId = msg.Chat.Id.ToString(CultureInfo.InvariantCulture);
        var text = msg.Text.Trim();

        try
        {
            if (text.StartsWith("/start", StringComparison.OrdinalIgnoreCase))
                await HandleStartAsync(chatId, text);
            else if (text.Equals("/help", StringComparison.OrdinalIgnoreCase))
                await _sender.SendMessageAsync(chatId, HelpText);
            else if (text.Equals("/unlink", StringComparison.OrdinalIgnoreCase))
                await HandleUnlinkAsync(chatId);
            else
                await HandleConversationAsync(chatId, text);
        }
        catch (Exception ex)
        {
            // Never let a handler failure bubble to the webhook (Telegram would retry endlessly).
            _logger.LogError(ex, "Failed to handle Telegram update for chat {ChatId}.", chatId);
            await _sender.SendMessageAsync(chatId, "Something went wrong handling that. Please try again.");
        }
    }

    private async Task HandleStartAsync(string chatId, string text)
    {
        // "/start" or "/start <code>".
        var parts = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var code = parts.Length > 1 ? parts[1] : null;

        if (string.IsNullOrWhiteSpace(code))
        {
            await _sender.SendMessageAsync(chatId,
                "👋 Welcome to TaskPilot!\n\nTo connect this chat to your account, open TaskPilot → " +
                "Settings → Telegram, copy your one-time code, then send:\n/start <code>");
            return;
        }

        var linked = await _link.LinkByCodeAsync(code, chatId);
        await _sender.SendMessageAsync(chatId, linked
            ? "✅ Linked! Just message me in plain language and I'll help. Send /help for what I can do."
            : "❌ That code is invalid or has expired. Get a fresh one in TaskPilot → Settings → Telegram.");
    }

    private async Task HandleUnlinkAsync(string chatId)
    {
        var user = await ResolveUserAsync(chatId);
        if (user is null)
        {
            await _sender.SendMessageAsync(chatId, "This chat isn't linked to a TaskPilot account.");
            return;
        }

        await _link.UnlinkAsync(user.Id);
        await _sender.SendMessageAsync(chatId, "🔌 Unlinked. Send /start <code> to reconnect later.");
    }

    private async Task HandleConversationAsync(string chatId, string text)
    {
        var user = await ResolveUserAsync(chatId);
        if (user is null)
        {
            await _sender.SendMessageAsync(chatId,
                "I don't know who you are yet. Link your account first: TaskPilot → Settings → Telegram, then send /start <code>.");
            return;
        }

        // Mirror the web app, where view-only accounts cannot use the assistant at all.
        if (user.Role == Role.Viewer)
        {
            await _sender.SendMessageAsync(chatId, "The assistant isn't available for view-only accounts.");
            return;
        }

        if (!_assistant.IsEnabled)
        {
            await _sender.SendMessageAsync(chatId, "The AI assistant isn't configured on the server right now.");
            return;
        }

        var result = await _assistant.AskAsync(user.Id, new[] { new ChatBotMessageDto { Role = "user", Content = text } });
        await _sender.SendMessageAsync(chatId, result.Succeeded && !string.IsNullOrWhiteSpace(result.Value)
            ? result.Value!
            : "Sorry, I couldn't answer that right now. Please try again in a moment.");
    }

    private async Task<LinkedUser?> ResolveUserAsync(string chatId) =>
        await _context.Users
            .Where(u => u.TelegramChatId == chatId)
            .Select(u => new LinkedUser(u.Id, u.Role))
            .FirstOrDefaultAsync();

    private record LinkedUser(Guid Id, Role Role);
}
