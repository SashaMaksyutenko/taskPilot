using FluentValidation;
using Taskpilot.API.DTOs.Projects;
using Taskpilot.API.Models;

namespace Taskpilot.API.Validators.Projects;

/// <summary>FluentValidation rules for <see cref="CreateTaskDto"/>.</summary>
public class CreateTaskValidator : AbstractValidator<CreateTaskDto>
{
    public CreateTaskValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MinimumLength(2).WithMessage("Title must be at least 2 characters.")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(10000).WithMessage("Description must not exceed 10000 characters.");

        RuleFor(x => x.Priority)
            .Must(BeValidPriority).WithMessage("Priority must be Low, Medium or High.");
    }

    /// <summary>Allows empty (defaults later) or a valid TaskPriority name.</summary>
    private static bool BeValidPriority(string? priority) =>
        string.IsNullOrWhiteSpace(priority) || Enum.TryParse<TaskPriority>(priority, ignoreCase: true, out _);
}
