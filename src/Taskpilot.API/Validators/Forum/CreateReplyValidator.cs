using FluentValidation;
using Taskpilot.API.DTOs.Forum;

namespace Taskpilot.API.Validators.Forum;

/// <summary>FluentValidation rules for <see cref="CreateReplyDto"/>.</summary>
public class CreateReplyValidator : AbstractValidator<CreateReplyDto>
{
    public CreateReplyValidator()
    {
        RuleFor(x => x.TopicId)
            .NotEmpty().WithMessage("TopicId is required.");

        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("Reply cannot be empty.")
            .MaximumLength(10000).WithMessage("Reply must not exceed 10000 characters.");
    }
}
