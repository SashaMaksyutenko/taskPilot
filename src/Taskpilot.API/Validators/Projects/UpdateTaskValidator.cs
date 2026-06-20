using FluentValidation;
using Taskpilot.API.DTOs.Projects;
using Taskpilot.API.Models;

namespace Taskpilot.API.Validators.Projects;

/// <summary>FluentValidation rules for <see cref="UpdateTaskDto"/>.</summary>
public class UpdateTaskValidator : AbstractValidator<UpdateTaskDto>
{
    public UpdateTaskValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MinimumLength(2).WithMessage("Title must be at least 2 characters.")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(10000).WithMessage("Description must not exceed 10000 characters.");

        RuleFor(x => x.Priority)
            .Must(p => string.IsNullOrWhiteSpace(p) || Enum.TryParse<TaskPriority>(p, ignoreCase: true, out _))
            .WithMessage("Priority must be Low, Medium or High.");
    }
}
