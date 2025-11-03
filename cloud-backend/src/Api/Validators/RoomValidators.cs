using FluentValidation;
using SafeSignal.Cloud.Api.DTOs;

namespace SafeSignal.Cloud.Api.Validators;

public class CreateRoomRequestValidator : AbstractValidator<CreateRoomRequest>
{
    public CreateRoomRequestValidator()
    {
        RuleFor(x => x.FloorId)
            .NotEmpty().WithMessage("Floor ID is required");

        RuleFor(x => x.RoomNumber)
            .NotEmpty().WithMessage("Room number is required")
            .MaximumLength(50).WithMessage("Room number must not exceed 50 characters");

        RuleFor(x => x.Name)
            .MaximumLength(200).WithMessage("Room name must not exceed 200 characters")
            .When(x => !string.IsNullOrEmpty(x.Name));

        RuleFor(x => x.RoomType)
            .MaximumLength(50).WithMessage("Room type must not exceed 50 characters")
            .When(x => !string.IsNullOrEmpty(x.RoomType));

        RuleFor(x => x.Capacity)
            .GreaterThan(0).WithMessage("Capacity must be greater than 0")
            .When(x => x.Capacity.HasValue);
    }
}

public class UpdateRoomRequestValidator : AbstractValidator<UpdateRoomRequest>
{
    public UpdateRoomRequestValidator()
    {
        RuleFor(x => x.RoomNumber)
            .MaximumLength(50).WithMessage("Room number must not exceed 50 characters")
            .When(x => !string.IsNullOrEmpty(x.RoomNumber));

        RuleFor(x => x.Name)
            .MaximumLength(200).WithMessage("Room name must not exceed 200 characters")
            .When(x => !string.IsNullOrEmpty(x.Name));

        RuleFor(x => x.RoomType)
            .MaximumLength(50).WithMessage("Room type must not exceed 50 characters")
            .When(x => !string.IsNullOrEmpty(x.RoomType));

        RuleFor(x => x.Capacity)
            .GreaterThan(0).WithMessage("Capacity must be greater than 0")
            .When(x => x.Capacity.HasValue);
    }
}
