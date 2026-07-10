using FluentValidation;
using Taskpilot.API.DTOs.Forum;

namespace Taskpilot.API.Validators.Forum;

/// <summary>FluentValidation rules for <see cref="EditReplyDto"/>.</summary>
public class EditReplyValidator : AbstractValidator<EditReplyDto>
{
    public EditReplyValidator()
    {
        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("Reply cannot be empty.")
            .MaximumLength(10000).WithMessage("Reply must not exceed 10000 characters.");
    }
}
