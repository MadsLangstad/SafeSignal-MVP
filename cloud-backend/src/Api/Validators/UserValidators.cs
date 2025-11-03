using FluentValidation;
using SafeSignal.Cloud.Api.DTOs;

namespace SafeSignal.Cloud.Api.Validators;

/// <summary>
/// Validates user update requests.
/// </summary>
public class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .MaximumLength(100)
            .WithMessage("First name must not exceed 100 characters")
            .Matches(@"^[a-zA-Z\s\-'\.]+$")
            .When(x => !string.IsNullOrWhiteSpace(x.FirstName))
            .WithMessage("First name contains invalid characters");

        RuleFor(x => x.LastName)
            .MaximumLength(100)
            .WithMessage("Last name must not exceed 100 characters")
            .Matches(@"^[a-zA-Z\s\-'\.]+$")
            .When(x => !string.IsNullOrWhiteSpace(x.LastName))
            .WithMessage("Last name contains invalid characters");

        RuleFor(x => x.Phone)
            .Must(BeValidPhoneNumber)
            .When(x => !string.IsNullOrWhiteSpace(x.Phone))
            .WithMessage("Phone number must be in valid format (e.g., +1234567890 or (123) 456-7890)");
    }

    private static bool BeValidPhoneNumber(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return true;

        // Remove common formatting characters
        var digitsOnly = new string(phone.Where(char.IsDigit).ToArray());

        // Phone number should have 10-15 digits (international format)
        return digitsOnly.Length >= 10 && digitsOnly.Length <= 15;
    }
}

/// <summary>
/// Validates push token registration requests.
/// </summary>
public class RegisterPushTokenRequestValidator : AbstractValidator<RegisterPushTokenRequest>
{
    public RegisterPushTokenRequestValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty()
            .WithMessage("Push token is required")
            .MaximumLength(500)
            .WithMessage("Push token must not exceed 500 characters");

        RuleFor(x => x.Platform)
            .NotEmpty()
            .WithMessage("Platform is required")
            .Must(platform => new[] { "ios", "android" }.Contains(platform.ToLowerInvariant()))
            .WithMessage("Platform must be either 'ios' or 'android'");
    }
}
