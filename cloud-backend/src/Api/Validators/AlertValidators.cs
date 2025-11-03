using FluentValidation;
using SafeSignal.Cloud.Api.DTOs;

namespace SafeSignal.Cloud.Api.Validators;

/// <summary>
/// Validates alert creation requests.
/// </summary>
public class CreateAlertRequestValidator : AbstractValidator<CreateAlertRequest>
{
    public CreateAlertRequestValidator()
    {
        RuleFor(x => x.AlertId)
            .NotEmpty()
            .WithMessage("Alert ID is required")
            .MaximumLength(100)
            .WithMessage("Alert ID must not exceed 100 characters");

        RuleFor(x => x.DeviceId)
            .NotEmpty()
            .When(x => x.DeviceId.HasValue)
            .WithMessage("Device ID must be a valid GUID");

        RuleFor(x => x.RoomId)
            .NotEmpty()
            .When(x => x.RoomId.HasValue)
            .WithMessage("Room ID must be a valid GUID");

        RuleFor(x => x.Severity)
            .IsInEnum()
            .WithMessage("Invalid alert severity");

        RuleFor(x => x.AlertType)
            .NotEmpty()
            .WithMessage("Alert type is required")
            .MaximumLength(50)
            .WithMessage("Alert type must not exceed 50 characters")
            .Must(type => new[] { "emergency", "lockdown", "evacuation", "medical", "fire", "security", "test" }
                .Contains(type.ToLowerInvariant()))
            .WithMessage("Alert type must be one of: emergency, lockdown, evacuation, medical, fire, security, test");

        RuleFor(x => x.Source)
            .IsInEnum()
            .WithMessage("Invalid alert source");

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
/// Validates alert trigger requests from mobile app.
/// </summary>
public class TriggerAlertRequestValidator : AbstractValidator<TriggerAlertRequest>
{
    public TriggerAlertRequestValidator()
    {
        RuleFor(x => x.BuildingId)
            .NotEmpty()
            .WithMessage("Building ID is required");

        RuleFor(x => x.DeviceId)
            .NotEmpty()
            .When(x => x.DeviceId.HasValue)
            .WithMessage("Device ID must be a valid GUID");

        RuleFor(x => x.RoomId)
            .NotEmpty()
            .When(x => x.RoomId.HasValue)
            .WithMessage("Room ID must be a valid GUID");

        RuleFor(x => x.Mode)
            .Must(mode => string.IsNullOrWhiteSpace(mode) || new[] { "silent", "audible", "lockdown", "evacuation" }
                .Contains(mode.ToLowerInvariant()))
            .WithMessage("Mode must be one of: silent, audible, lockdown, evacuation");

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
/// Validates alert update requests.
/// </summary>
public class UpdateAlertRequestValidator : AbstractValidator<UpdateAlertRequest>
{
    public UpdateAlertRequestValidator()
    {
        RuleFor(x => x.Status)
            .IsInEnum()
            .WithMessage("Invalid alert status");

        RuleFor(x => x.ResolvedAt)
            .LessThanOrEqualTo(DateTime.UtcNow.AddMinutes(1))
            .When(x => x.ResolvedAt.HasValue)
            .WithMessage("Resolved time cannot be in the future");
    }
}
