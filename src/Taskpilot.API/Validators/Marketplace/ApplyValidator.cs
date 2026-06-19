using FluentValidation;
using Taskpilot.API.DTOs.Marketplace;

namespace Taskpilot.API.Validators.Marketplace;

/// <summary>FluentValidation rules for <see cref="ApplyDto"/>.</summary>
public class ApplyValidator : AbstractValidator<ApplyDto>
{
    public ApplyValidator()
    {
        RuleFor(x => x.TaskId)
            .NotEmpty().WithMessage("TaskId is required.");

        RuleFor(x => x.CoverLetter)
            .NotEmpty().WithMessage("Cover letter is required.")
            .MaximumLength(2000).WithMessage("Cover letter must not exceed 2000 characters.");

        RuleFor(x => x.ProposedRate)
            .GreaterThan(0).WithMessage("Proposed rate must be greater than 0.");
    }
}
