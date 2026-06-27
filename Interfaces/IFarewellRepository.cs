using PortfolioApi.Models;

namespace PortfolioApi.Interfaces;

/// <summary>
/// DATA layer for farewell RSVPs. Pure EF Core — no hashing, sanitization,
/// plot-assignment, or HTTP.
/// </summary>
public interface IFarewellRepository
{
    Task<IReadOnlyList<FarewellRsvp>> GetAllAsync();                 // oldest first, no-tracking
    Task<FarewellRsvp?>               FindByOperatorAsync(string operatorHash);  // tracked (for edit)
    Task<int>                         CountAsync();
    Task                              AddAsync(FarewellRsvp rsvp);
    Task                              UpdateAsync(FarewellRsvp rsvp);
}
