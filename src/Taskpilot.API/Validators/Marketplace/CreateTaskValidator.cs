using FluentValidation;
using Taskpilot.API.DTOs.Marketplace;

namespace Taskpilot.API.Validators.Marketplace;

/// <summary>FluentValidation rules for <see cref="CreateTaskDto"/>.</summary>
public class CreateTaskValidator : AbstractValidator<CreateTaskDto>
{
    public CreateTaskValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MinimumLength(3).WithMessage("Title must be at least 3 characters.")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required.")
            .MaximumLength(10000).WithMessage("Description must not exceed 10000 characters.");

        RuleFor(x => x.Budget)
            .GreaterThan(0).WithMessage("Budget must be greater than 0.");

        RuleFor(x => x.RequiredSkills)
            .MaximumLength(500).WithMessage("Skills must not exceed 500 characters.");
    }
}
