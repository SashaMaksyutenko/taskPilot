using FluentValidation;
using Taskpilot.API.DTOs.Auth;

namespace Taskpilot.API.Validators.Auth;

/// <summary>
/// FluentValidation rules for <see cref="LoginDto"/>.
/// Login validation only checks that the credentials are present and well-formed.
/// Password complexity is NOT re-checked here — that belongs to registration; on
/// login the password just has to match the stored hash.
/// </summary>
public class LoginValidator : AbstractValidator<LoginDto>
{
    /// <summary>
    /// Defines the validation rules for the login payload.
    /// </summary>
    public LoginValidator()
    {
        // Email: required and well-formed.
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email format is invalid.");

        // Password: required (no complexity rules on login).
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}
