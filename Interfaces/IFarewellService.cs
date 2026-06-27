using PortfolioApi.DTOs;

namespace PortfolioApi.Interfaces;

/// <summary>
/// BUSINESS layer for farewell RSVPs. Owns identity hashing, sanitization, the
/// attending→logistics rule, the ring-layout plot assignment, and the
/// one-monument-per-visitor rule. The controller never sees the DB.
/// </summary>
public interface IFarewellService
{
    Task<IReadOnlyList<FarewellPlantView>>      GetPlantsAsync(string? rawOperatorToken);
    Task<FarewellWriteResult>                  SaveAsync(CreateFarewellRsvpDto dto, string? rawOperatorToken);
    Task<IReadOnlyList<FarewellRsvpAdminView>> GetAllForAdminAsync();
}
