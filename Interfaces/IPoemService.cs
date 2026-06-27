using PortfolioApi.DTOs;
using PortfolioApi.Models;

namespace PortfolioApi.Interfaces;

/// <summary>
/// BUSINESS layer — owns the rules (sanitization, DTO→entity mapping) and
/// orchestrates the repository. The controller talks only to this.
/// </summary>
public interface IPoemService
{
    Task<IEnumerable<Poem>> ListAsync();
    Task<Poem?>             GetAsync(int id);
    Task<Poem>             CreateAsync(PoemDto dto);
    Task<Poem?>            UpdateAsync(int id, PoemDto dto);   // null → not found
    Task<bool>            DeleteAsync(int id);                // false → not found
}
