using FluentValidation;
using Taskpilot.API.DTOs.Admin;

namespace Taskpilot.API.Validators.Admin;

/// <summary>FluentValidation rules for <see cref="CreateAppealDto"/>.</summary>
public class CreateAppealValidator : AbstractValidator<CreateAppealDto>
{
    public CreateAppealValidator()
    {
        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("Please explain your appeal.")
            .MinimumLength(10).WithMessage("The appeal must be at least 10 characters.")
            .MaximumLength(2000).WithMessage("The appeal must not exceed 2000 characters.");
    }
}
