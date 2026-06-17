using FluentValidation;
using Taskpilot.API.DTOs.Auth;

namespace Taskpilot.API.Validators.Auth;

/// <summary>
/// FluentValidation rules for <see cref="RegisterDto"/>.
/// Runs before any business logic so invalid input is rejected early
/// with clear, client-friendly error messages.
/// </summary>
public class RegisterValidator : AbstractValidator<RegisterDto>
{
    // Password policy constants (avoid magic numbers in the rules below).
    private const int MinPasswordLength = 8;
    private const int MaxPasswordLength = 100;
    private const int MaxNameLength = 100;
    private const int MaxEmailLength = 256;

    /// <summary>
    /// Defines all validation rules for the registration payload.
    /// </summary>
    public RegisterValidator()
    {
        // Name: required, trimmed length between 2 and 100 characters.
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MinimumLength(2).WithMessage("Name must be at least 2 characters.")
            .MaximumLength(MaxNameLength).WithMessage($"Name must not exceed {MaxNameLength} characters.");

        // Email: required, valid format, within the DB column limit.
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email format is invalid.")
            .MaximumLength(MaxEmailLength).WithMessage($"Email must not exceed {MaxEmailLength} characters.");

        // Password: required, length bounds, and basic complexity
        // (at least one lowercase letter, one uppercase letter and one digit).
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(MinPasswordLength).WithMessage($"Password must be at least {MinPasswordLength} characters.")
            .MaximumLength(MaxPasswordLength).WithMessage($"Password must not exceed {MaxPasswordLength} characters.")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.");
    }
}
