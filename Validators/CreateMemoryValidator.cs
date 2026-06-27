using FluentValidation;
using PortfolioApi.DTOs;

namespace PortfolioApi.Validators;

/// <summary>Allow-list validation (auto-registered via AddValidatorsFromAssembly).</summary>
public class CreateMemoryValidator : AbstractValidator<CreateMemoryDto>
{
    // Accepts both the legacy zone keys and the specific clickable-building keys
    // sent by the frontend (SCENE_LAYOUT). Keep in sync with PLACE_KEYS / BUILDING_LABEL.
    private static readonly string[] Landmarks =
    {
        // legacy zones
        "tree", "ship", "village", "castle", "plaza",
        // clickable buildings
        "sakura", "torii", "bagan", "ferris_wheel", "jp_castle",
        "castle_sakura", "plaza_night", "hospital", "london_university",
    };

    public CreateMemoryValidator()
    {
        RuleFor(x => x.Author).NotEmpty().MaximumLength(40);
        RuleFor(x => x.Message).NotEmpty().MaximumLength(240);
        RuleFor(x => x.Landmark).Must(l => Landmarks.Contains(l)).WithMessage("Invalid landmark.");
        RuleFor(x => x.PositionX).InclusiveBetween(-200f, 200f);
        RuleFor(x => x.PositionY).InclusiveBetween(-50f, 100f);
        RuleFor(x => x.PositionZ).InclusiveBetween(-200f, 200f);
    }
}
