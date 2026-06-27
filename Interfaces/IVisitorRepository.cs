using PortfolioApi.DTOs;

namespace PortfolioApi.Interfaces;

/// <summary>
/// DATA layer for the visitor counter. Owns the raw-SQL storage (auto-creates the
/// two tables on first use) and all reads/writes. No HTTP or business rules.
/// </summary>
public interface IVisitorRepository
{
    Task<long>                       GetTotalAsync();
    Task                             IncrementTotalAsync();
    Task                             IncrementCountryAsync(string country);  // pre-normalized
    Task<IReadOnlyList<CountryCount>> GetCountriesAsync();
}
