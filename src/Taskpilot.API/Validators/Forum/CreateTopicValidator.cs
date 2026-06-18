using FluentValidation;
using Taskpilot.API.DTOs.Forum;

namespace Taskpilot.API.Validators.Forum;

/// <summary>FluentValidation rules for <see cref="CreateTopicDto"/>.</summary>
public class CreateTopicValidator : AbstractValidator<CreateTopicDto>
{
    public CreateTopicValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MinimumLength(3).WithMessage("Title must be at least 3 characters.")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters.");

        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("Body is required.")
            .MaximumLength(10000).WithMessage("Body must not exceed 10000 characters.");
    }
}
