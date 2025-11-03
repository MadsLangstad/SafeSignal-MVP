using FluentValidation;
using SafeSignal.Cloud.Api.DTOs;

namespace SafeSignal.Cloud.Api.Validators;

/// <summary>
/// Validates building creation requests.
/// </summary>
public class CreateBuildingRequestValidator : AbstractValidator<CreateBuildingRequest>
{
    public CreateBuildingRequestValidator()
    {
        RuleFor(x => x.SiteId)
            .NotEmpty()
            .WithMessage("Site ID is required");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Building name is required")
            .MaximumLength(200)
            .WithMessage("Building name must not exceed 200 characters")
            .Matches(@"^[a-zA-Z0-9\s\-\.,'&#]+$")
            .WithMessage("Building name contains invalid characters");

        RuleFor(x => x.Address)
            .MaximumLength(500)
            .WithMessage("Address must not exceed 500 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.Address));

        RuleFor(x => x.FloorCount)
            .GreaterThan(0)
            .WithMessage("Floor count must be greater than 0")
            .LessThanOrEqualTo(200)
            .WithMessage("Floor count must not exceed 200");
    }
}

/// <summary>
/// Validates room creation requests.
/// </summary>
public class CreateRoomRequestValidator : AbstractValidator<CreateRoomRequest>
{
    public CreateRoomRequestValidator()
    {
        RuleFor(x => x.FloorId)
            .NotEmpty()
            .WithMessage("Floor ID is required");

        RuleFor(x => x.RoomNumber)
            .NotEmpty()
            .WithMessage("Room number is required")
            .MaximumLength(20)
            .WithMessage("Room number must not exceed 20 characters")
            .Matches(@"^[a-zA-Z0-9\-\.]+$")
            .WithMessage("Room number must contain only letters, numbers, hyphens, and periods");

        RuleFor(x => x.Name)
            .MaximumLength(200)
            .WithMessage("Room name must not exceed 200 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.Name));

        RuleFor(x => x.RoomType)
            .MaximumLength(50)
            .WithMessage("Room type must not exceed 50 characters")
            .Must(type => string.IsNullOrWhiteSpace(type) || new[] { 
                "classroom", "office", "lab", "auditorium", "cafeteria", 
                "gym", "library", "hallway", "restroom", "storage", "other" 
            }.Contains(type.ToLowerInvariant()))
            .When(x => !string.IsNullOrWhiteSpace(x.RoomType))
            .WithMessage("Invalid room type");

        RuleFor(x => x.Capacity)
            .GreaterThan(0)
            .WithMessage("Capacity must be greater than 0")
            .LessThanOrEqualTo(10000)
            .WithMessage("Capacity must not exceed 10,000")
            .When(x => x.Capacity.HasValue);
    }
}

/// <summary>
/// Validates room update requests.
/// </summary>
public class UpdateRoomRequestValidator : AbstractValidator<UpdateRoomRequest>
{
    public UpdateRoomRequestValidator()
    {
        RuleFor(x => x.RoomNumber)
            .MaximumLength(20)
            .WithMessage("Room number must not exceed 20 characters")
            .Matches(@"^[a-zA-Z0-9\-\.]+$")
            .When(x => !string.IsNullOrWhiteSpace(x.RoomNumber))
            .WithMessage("Room number must contain only letters, numbers, hyphens, and periods");

        RuleFor(x => x.Name)
            .MaximumLength(200)
            .WithMessage("Room name must not exceed 200 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.Name));

        RuleFor(x => x.RoomType)
            .MaximumLength(50)
            .WithMessage("Room type must not exceed 50 characters")
            .Must(type => string.IsNullOrWhiteSpace(type) || new[] { 
                "classroom", "office", "lab", "auditorium", "cafeteria", 
                "gym", "library", "hallway", "restroom", "storage", "other" 
            }.Contains(type!.ToLowerInvariant()))
            .When(x => !string.IsNullOrWhiteSpace(x.RoomType))
            .WithMessage("Invalid room type");

        RuleFor(x => x.Capacity)
            .GreaterThan(0)
            .WithMessage("Capacity must be greater than 0")
            .LessThanOrEqualTo(10000)
            .WithMessage("Capacity must not exceed 10,000")
            .When(x => x.Capacity.HasValue);
    }
}
