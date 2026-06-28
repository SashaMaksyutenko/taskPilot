using FluentValidation;
using Taskpilot.API.DTOs.Admin;

namespace Taskpilot.API.Validators.Admin;

/// <summary>FluentValidation rules for <see cref="IssueWarningDto"/>.</summary>
public class IssueWarningValidator : AbstractValidator<IssueWarningDto>
{
    public IssueWarningValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("A reason is required.")
            .MinimumLength(3).WithMessage("The reason must be at least 3 characters.")
            .MaximumLength(1000).WithMessage("The reason must not exceed 1000 characters.");
    }
}
