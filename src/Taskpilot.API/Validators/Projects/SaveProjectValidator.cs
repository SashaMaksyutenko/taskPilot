using FluentValidation;
using Taskpilot.API.DTOs.Projects;

namespace Taskpilot.API.Validators.Projects;

/// <summary>FluentValidation rules for <see cref="SaveProjectDto"/> (create &amp; update).</summary>
public class SaveProjectValidator : AbstractValidator<SaveProjectDto>
{
    public SaveProjectValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Project name is required.")
            .MinimumLength(2).WithMessage("Project name must be at least 2 characters.")
            .MaximumLength(150).WithMessage("Project name must not exceed 150 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(5000).WithMessage("Description must not exceed 5000 characters.");

        RuleFor(x => x.Color)
            .MaximumLength(20).WithMessage("Color must not exceed 20 characters.");
    }
}
