using PortfolioApi.DTOs;

namespace PortfolioApi.Interfaces;

/// <summary>
/// BUSINESS layer for Sanctuary memories. Owns identity hashing, privacy masking,
/// sanitization and the one-memory-per-operator rule. The controller passes the
/// raw operator token (an HTTP concern) and never sees the DB.
/// </summary>
public interface IMemoryService
{
    Task<IReadOnlyList<MemoryView>>      GetMemoriesAsync(string? rawOperatorToken, bool isAdmin);
    Task<MemoryWriteResult>             SaveAsync(CreateMemoryDto dto, string? rawOperatorToken);
    Task<IReadOnlyList<AdminMemoryView>> GetAllForAdminAsync();
}
