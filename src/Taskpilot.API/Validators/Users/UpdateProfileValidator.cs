using FluentValidation;
using Taskpilot.API.DTOs.Users;

namespace Taskpilot.API.Validators.Users;

/// <summary>FluentValidation rules for <see cref="UpdateProfileDto"/>.</summary>
public class UpdateProfileValidator : AbstractValidator<UpdateProfileDto>
{
    public UpdateProfileValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MinimumLength(2).WithMessage("Name must be at least 2 characters.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");

        RuleFor(x => x.Title)
            .MaximumLength(100).WithMessage("Title must not exceed 100 characters.");

        RuleFor(x => x.Bio)
            .MaximumLength(1000).WithMessage("Bio must not exceed 1000 characters.");

        RuleFor(x => x.Location)
            .MaximumLength(100).WithMessage("Location must not exceed 100 characters.");

        RuleFor(x => x.Website)
            .MaximumLength(200).WithMessage("Website must not exceed 200 characters.");

        RuleFor(x => x.LinkedIn)
            .MaximumLength(200).WithMessage("LinkedIn must not exceed 200 characters.");

        RuleFor(x => x.GitHub)
            .MaximumLength(200).WithMessage("GitHub must not exceed 200 characters.");

        RuleFor(x => x.Phone)
            .MaximumLength(30).WithMessage("Phone must not exceed 30 characters.");
    }
}
