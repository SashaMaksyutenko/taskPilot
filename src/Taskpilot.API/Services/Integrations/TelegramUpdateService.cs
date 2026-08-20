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

    private const string WelcomeText =
        "Welcome to TaskPilot! Open Settings in the app, tap \"Connect Telegram\", and send me the code shown there.";

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
            if (text.StartsWith("/help", StringComparison.OrdinalIgnoreCase))
            {
                await _sender.SendMessageAsync(chatId, HelpText);
            }
            else if (text.StartsWith("/unlink", StringComparison.OrdinalIgnoreCase))
            {
                await HandleUnlinkAsync(chatId);
            }
            else if (text.StartsWith("/start", StringComparison.OrdinalIgnoreCase))
            {
                // "/start" or "/start <code>".
                var parts = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length > 1)
                    await TryLinkAsync(chatId, parts[1]);
                else
                    await _sender.SendMessageAsync(chatId, WelcomeText);
            }
            else
            {
                await HandleFreeTextAsync(chatId, text);
            }
        }
        catch (Exception ex)
        {
            // Never let a handler failure bubble up (Telegram would retry the update endlessly).
            _logger.LogError(ex, "Failed to handle Telegram update for chat {ChatId}.", chatId);
            await _sender.SendMessageAsync(chatId, "Something went wrong handling that. Please try again.");
        }
    }

    /// <summary>
    /// Non-command text: a linked user is talking to the assistant; an unlinked chat is trying to
    /// link, so we treat the message as a bare code (that's what the welcome asks them to send).
    /// </summary>
    private async Task HandleFreeTextAsync(string chatId, string text)
    {
        var user = await ResolveUserAsync(chatId);
        if (user is null)
        {
            var code = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[^1];
            await TryLinkAsync(chatId, code);
            return;
        }

        // A banned or closed account must lose bot access too — the web ban revokes sessions,
        // so the Telegram channel can't stay an open back door into the write-capable assistant.
        if (!user.IsActive)
        {
            await _sender.SendMessageAsync(chatId, "This account is no longer active.");
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

    private async Task TryLinkAsync(string chatId, string code)
    {
        var linked = await _link.LinkByCodeAsync(code, chatId);
        await _sender.SendMessageAsync(chatId, linked
            ? "✅ Your Telegram is now linked to TaskPilot. You'll get notifications here — and you can just message me, " +
              "e.g. \"what tasks are overdue?\". Send /help for more."
            : "That link code is invalid or expired. Generate a new one in TaskPilot settings.");
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
        await _sender.SendMessageAsync(chatId, "🔌 Unlinked. Send your code again to reconnect later.");
    }

    private async Task<LinkedUser?> ResolveUserAsync(string chatId) =>
        await _context.Users
            .Where(u => u.TelegramChatId == chatId)
            .Select(u => new LinkedUser(u.Id, u.Role, u.IsActive))
            .FirstOrDefaultAsync();

    private record LinkedUser(Guid Id, Role Role, bool IsActive);
}
