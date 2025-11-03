using FluentValidation;
using SafeSignal.Cloud.Api.DTOs;

namespace SafeSignal.Cloud.Api.Validators;

public class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .MaximumLength(100).WithMessage("First name must not exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.FirstName));

        RuleFor(x => x.LastName)
            .MaximumLength(100).WithMessage("Last name must not exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.LastName));

        RuleFor(x => x.Phone)
            .MaximumLength(20).WithMessage("Phone number must not exceed 20 characters")
            .Matches(@"^\+?[\d\s\-\(\)]+$").WithMessage("Invalid phone number format")
            .When(x => !string.IsNullOrEmpty(x.Phone));
    }
}

public class RegisterPushTokenRequestValidator : AbstractValidator<RegisterPushTokenRequest>
{
    public RegisterPushTokenRequestValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Push token is required")
            .MaximumLength(500).WithMessage("Token must not exceed 500 characters");

        RuleFor(x => x.Platform)
            .NotEmpty().WithMessage("Platform is required")
            .Must(p => p == "ios" || p == "android")
            .WithMessage("Platform must be either 'ios' or 'android'");
    }
}
