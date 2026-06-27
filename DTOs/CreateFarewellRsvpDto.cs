namespace PortfolioApi.DTOs;

/// <summary>Incoming payload for a farewell RSVP. Position is assigned server-side.</summary>
public class CreateFarewellRsvpDto
{
    public bool Attending { get; set; } = true;          // can they join the party?
    public string Name { get; set; } = string.Empty;
    public string DatesAvailable { get; set; } = string.Empty;  // optional (empty when not attending)
    public string FoodPreference { get; set; } = string.Empty;  // optional (empty when not attending)
    public string Message { get; set; } = string.Empty;
    public string PlantType { get; set; } = "sakura";
}
