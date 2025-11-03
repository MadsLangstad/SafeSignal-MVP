using FluentValidation;
using SafeSignal.Cloud.Api.DTOs;
using SafeSignal.Cloud.Core.Interfaces;

namespace SafeSignal.Cloud.Api.Validators;

/// <summary>
/// Validates password change requests using configured password policy.
/// </summary>
public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator(IPasswordValidator passwordValidator)
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty()
            .WithMessage("Current password is required");

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .WithMessage("New password is required")
            .Must((request, newPassword) => newPassword != request.CurrentPassword)
            .WithMessage("New password must be different from current password")
            .Custom((newPassword, context) =>
            {
                var result = passwordValidator.Validate(newPassword);
                if (!result.IsValid)
                {
                    foreach (var error in result.Errors)
                    {
                        context.AddFailure("NewPassword", error);
                    }
                }
            });
    }
}
