using FluentValidation;
using SafeSignal.Cloud.Api.DTOs;

namespace SafeSignal.Cloud.Api.Validators;

/// <summary>
/// Validates organization creation requests.
/// </summary>
public class CreateOrganizationRequestValidator : AbstractValidator<CreateOrganizationRequest>
{
    public CreateOrganizationRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Organization name is required")
            .MaximumLength(200)
            .WithMessage("Organization name must not exceed 200 characters")
            .Matches(@"^[a-zA-Z0-9\s\-\.,'&]+$")
            .WithMessage("Organization name contains invalid characters");

        RuleFor(x => x.Slug)
            .NotEmpty()
            .WithMessage("Organization slug is required")
            .MaximumLength(100)
            .WithMessage("Slug must not exceed 100 characters")
            .Matches(@"^[a-z0-9\-]+$")
            .WithMessage("Slug must contain only lowercase letters, numbers, and hyphens")
            .Must(slug => !slug.StartsWith("-") && !slug.EndsWith("-"))
            .WithMessage("Slug cannot start or end with a hyphen");

        RuleFor(x => x.Metadata)
            .Must(BeValidJson)
            .When(x => !string.IsNullOrWhiteSpace(x.Metadata))
            .WithMessage("Metadata must be valid JSON");
    }

    private static bool BeValidJson(string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata))
            return true;

        try
        {
            System.Text.Json.JsonDocument.Parse(metadata);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Validates organization update requests.
/// </summary>
public class UpdateOrganizationRequestValidator : AbstractValidator<UpdateOrganizationRequest>
{
    public UpdateOrganizationRequestValidator()
    {
        RuleFor(x => x.Name)
            .MaximumLength(200)
            .WithMessage("Organization name must not exceed 200 characters")
            .Matches(@"^[a-zA-Z0-9\s\-\.,'&]+$")
            .When(x => !string.IsNullOrWhiteSpace(x.Name))
            .WithMessage("Organization name contains invalid characters");

        RuleFor(x => x.Slug)
            .MaximumLength(100)
            .WithMessage("Slug must not exceed 100 characters")
            .Matches(@"^[a-z0-9\-]+$")
            .When(x => !string.IsNullOrWhiteSpace(x.Slug))
            .WithMessage("Slug must contain only lowercase letters, numbers, and hyphens")
            .Must(slug => !slug!.StartsWith("-") && !slug.EndsWith("-"))
            .When(x => !string.IsNullOrWhiteSpace(x.Slug))
            .WithMessage("Slug cannot start or end with a hyphen");

        RuleFor(x => x.Status)
            .IsInEnum()
            .When(x => x.Status.HasValue)
            .WithMessage("Invalid organization status");

        RuleFor(x => x.Metadata)
            .Must(BeValidJson)
            .When(x => !string.IsNullOrWhiteSpace(x.Metadata))
            .WithMessage("Metadata must be valid JSON");
    }

    private static bool BeValidJson(string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata))
            return true;

        try
        {
            System.Text.Json.JsonDocument.Parse(metadata);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
