using FluentValidation;
using PortfolioApi.DTOs;

namespace PortfolioApi.Validators;

/// <summary>Allow-list validation (auto-registered via AddValidatorsFromAssembly).</summary>
public class CreateFarewellRsvpValidator : AbstractValidator<CreateFarewellRsvpDto>
{
    private static readonly string[] Plants = { "sakura", "orchid" };

    public CreateFarewellRsvpValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(40);
        RuleFor(x => x.Message).NotEmpty().MaximumLength(240);
        RuleFor(x => x.DatesAvailable).MaximumLength(120);
        RuleFor(x => x.FoodPreference).MaximumLength(80);
        RuleFor(x => x.PlantType)
            .Must(p => Plants.Contains((p ?? string.Empty).Trim().ToLowerInvariant()))
            .WithMessage("Invalid plant type.");
    }
}
