using System.ComponentModel.DataAnnotations;

namespace PortfolioApi.DTOs.Interaction;

/// <summary>Body for POST /api/Articles/{id}/reactions.</summary>
public class ReactionRequestDto
{
    [Required, MaxLength(20)]
    public string Reaction { get; set; } = string.Empty;
}
