using FluentValidation;
using Taskpilot.API.DTOs.Chat;

namespace Taskpilot.API.Validators.Chat;

/// <summary>FluentValidation rules for <see cref="CreateGroupConversationDto"/>.</summary>
public class CreateGroupConversationValidator : AbstractValidator<CreateGroupConversationDto>
{
    public CreateGroupConversationValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Group name is required.")
            .MinimumLength(2).WithMessage("Group name must be at least 2 characters.")
            .MaximumLength(150).WithMessage("Group name must not exceed 150 characters.");

        RuleFor(x => x.ParticipantIds)
            .NotNull().WithMessage("ParticipantIds is required.")
            .Must(ids => ids.Count > 0)
            .WithMessage("A group must include at least one other participant.");
    }
}
