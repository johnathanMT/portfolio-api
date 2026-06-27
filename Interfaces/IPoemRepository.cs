using PortfolioApi.Models;

namespace PortfolioApi.Interfaces;

/// <summary>
/// DATA layer — owns all EF Core access for poems. Knows nothing about HTTP,
/// validation, or sanitization (those belong in the service/controller).
/// </summary>
public interface IPoemRepository
{
    Task<IEnumerable<Poem>> GetAllAsync();
    Task<Poem?>             GetByIdAsync(int id);
    Task<Poem>             CreateAsync(Poem poem);
    Task<Poem?>            UpdateAsync(int id, Poem changes);   // null → not found
    Task<bool>            DeleteAsync(int id);                 // false → not found
}
