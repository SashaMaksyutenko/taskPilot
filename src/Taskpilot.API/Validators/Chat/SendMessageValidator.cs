using FluentValidation;
using Taskpilot.API.DTOs.Chat;

namespace Taskpilot.API.Validators.Chat;

/// <summary>FluentValidation rules for <see cref="SendMessageDto"/>.</summary>
public class SendMessageValidator : AbstractValidator<SendMessageDto>
{
    private const int MaxContentLength = 4000;

    public SendMessageValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty().WithMessage("ConversationId is required.");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Message cannot be empty.")
            .MaximumLength(MaxContentLength)
            .WithMessage($"Message must not exceed {MaxContentLength} characters.");
    }
}
