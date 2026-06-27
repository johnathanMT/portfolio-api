namespace PortfolioApi.DTOs;

/// <summary>Read view models for farewell RSVPs. The service produces these; the
/// controller maps them to the final JSON shape.</summary>

// Public-facing monument for the 3D world (no event logistics).
public record FarewellPlantView(
    int Id, string Name, string Message, string PlantType,
    float X, float Y, float Z, DateTime CreatedAt, bool Mine);

// Admin view — includes the logistics (attending / dates / food) for planning.
public record FarewellRsvpAdminView(
    int Id, string Name, string Message, bool Attending,
    string DatesAvailable, string FoodPreference, string PlantType,
    float X, float Y, float Z, DateTime CreatedAt);

// Result of a create/edit, so the controller can choose 200 / 201 / 400.
public record FarewellWriteResult(
    bool Ok, int Id, string Name, string PlantType,
    float X, float Y, float Z, bool Edited, string? Error);
