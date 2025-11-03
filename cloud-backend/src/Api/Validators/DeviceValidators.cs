using FluentValidation;
using SafeSignal.Cloud.Api.DTOs;

namespace SafeSignal.Cloud.Api.Validators;

/// <summary>
/// Validates device registration requests.
/// </summary>
public class RegisterDeviceRequestValidator : AbstractValidator<RegisterDeviceRequest>
{
    public RegisterDeviceRequestValidator()
    {
        RuleFor(x => x.DeviceId)
            .NotEmpty()
            .WithMessage("Device ID is required")
            .MaximumLength(100)
            .WithMessage("Device ID must not exceed 100 characters")
            .Matches(@"^[a-zA-Z0-9\-_]+$")
            .WithMessage("Device ID must contain only letters, numbers, hyphens, and underscores");

        RuleFor(x => x.SerialNumber)
            .MaximumLength(100)
            .WithMessage("Serial number must not exceed 100 characters")
            .Matches(@"^[a-zA-Z0-9\-]+$")
            .When(x => !string.IsNullOrWhiteSpace(x.SerialNumber))
            .WithMessage("Serial number must contain only letters, numbers, and hyphens");

        RuleFor(x => x.MacAddress)
            .Must(BeValidMacAddress)
            .When(x => !string.IsNullOrWhiteSpace(x.MacAddress))
            .WithMessage("MAC address must be in valid format (e.g., AA:BB:CC:DD:EE:FF or AA-BB-CC-DD-EE-FF)");

        RuleFor(x => x.HardwareVersion)
            .MaximumLength(50)
            .WithMessage("Hardware version must not exceed 50 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.HardwareVersion));

        RuleFor(x => x.Metadata)
            .Must(BeValidJson)
            .When(x => !string.IsNullOrWhiteSpace(x.Metadata))
            .WithMessage("Metadata must be valid JSON");
    }

    private static bool BeValidMacAddress(string? macAddress)
    {
        if (string.IsNullOrWhiteSpace(macAddress))
            return true;

        // Support both : and - separators
        var pattern = @"^([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2})$";
        return System.Text.RegularExpressions.Regex.IsMatch(macAddress, pattern);
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
/// Validates device update requests.
/// </summary>
public class UpdateDeviceRequestValidator : AbstractValidator<UpdateDeviceRequest>
{
    public UpdateDeviceRequestValidator()
    {
        RuleFor(x => x.RoomId)
            .NotEmpty()
            .When(x => x.RoomId.HasValue)
            .WithMessage("Room ID must be a valid GUID");

        RuleFor(x => x.FirmwareVersion)
            .MaximumLength(50)
            .WithMessage("Firmware version must not exceed 50 characters")
            .Matches(@"^[0-9]+\.[0-9]+\.[0-9]+(-[a-zA-Z0-9]+)?$")
            .When(x => !string.IsNullOrWhiteSpace(x.FirmwareVersion))
            .WithMessage("Firmware version must be in semantic versioning format (e.g., 1.2.3 or 1.2.3-beta)");

        RuleFor(x => x.Status)
            .IsInEnum()
            .When(x => x.Status.HasValue)
            .WithMessage("Invalid device status");

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
