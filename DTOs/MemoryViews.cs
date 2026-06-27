namespace PortfolioApi.DTOs;

/// <summary>
/// Read view models returned by the service. The controller maps these to the
/// final JSON shape, so the service owns the BUSINESS decision (e.g. whether the
/// message is masked) while the controller owns presentation.
/// </summary>

// Public-facing memory — Message is already masked by the service when needed.
public record MemoryView(
    int Id, string Author, string Landmark,
    float X, float Y, float Z,
    DateTime CreatedAt, bool Mine, string Message);

// Admin view — full, unmasked message; no "mine" flag.
public record AdminMemoryView(
    int Id, string Author, string Message, string Landmark,
    float X, float Y, float Z, DateTime CreatedAt);

// Result of a create/edit, so the controller can choose 200 / 201 / 400.
public record MemoryWriteResult(bool Ok, int Id, bool Edited, string? Error);
