using FluentValidation;
using PortfolioApi.DTOs;

namespace PortfolioApi.Validators;

/// <summary>Allow-list validation (auto-registered via AddValidatorsFromAssembly).</summary>
public class PoemDtoValidator : AbstractValidator<PoemDto>
{
    public PoemDtoValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Subtitle).MaximumLength(80);
        RuleFor(x => x.Content).NotEmpty().MaximumLength(4000);
    }
}
