using FluentValidation;
using Taskpilot.API.DTOs.Projects;

namespace Taskpilot.API.Validators.Projects;

/// <summary>FluentValidation rules for <see cref="CreateCommentDto"/>.</summary>
public class CreateCommentValidator : AbstractValidator<CreateCommentDto>
{
    public CreateCommentValidator()
    {
        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("Comment cannot be empty.")
            .MaximumLength(5000).WithMessage("Comment must not exceed 5000 characters.");
    }
}
