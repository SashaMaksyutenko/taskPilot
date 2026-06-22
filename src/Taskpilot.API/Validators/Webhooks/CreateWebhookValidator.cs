using FluentValidation;
using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Webhooks;

namespace Taskpilot.API.Validators.Webhooks;

/// <summary>FluentValidation rules for <see cref="CreateWebhookDto"/>.</summary>
public class CreateWebhookValidator : AbstractValidator<CreateWebhookDto>
{
    public CreateWebhookValidator()
    {
        RuleFor(x => x.Url)
            .NotEmpty().WithMessage("Url is required.")
            .MaximumLength(500)
            .Must(BeHttpUrl).WithMessage("Url must be a valid http(s) URL.");

        RuleFor(x => x.Event)
            .NotEmpty().WithMessage("Event is required.")
            .Must(e => WebhookEvents.All.Contains(e))
            .WithMessage($"Event must be one of: {string.Join(", ", WebhookEvents.All)}.");
    }

    private static bool BeHttpUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
