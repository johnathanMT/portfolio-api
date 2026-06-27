using PortfolioApi.Models;

namespace PortfolioApi.Interfaces;

/// <summary>
/// DATA layer for Sanctuary memory tags. Pure EF Core access — knows nothing
/// about hashing, masking, sanitization, or HTTP.
/// </summary>
public interface IMemoryRepository
{
    Task<IReadOnlyList<MemoryTag>> GetAllAsync();              // newest first, no-tracking
    Task<MemoryTag?>               FindByOperatorAsync(string operatorHash);  // tracked (for edit)
    Task                           AddAsync(MemoryTag tag);
    Task                           UpdateAsync(MemoryTag tag);
}
