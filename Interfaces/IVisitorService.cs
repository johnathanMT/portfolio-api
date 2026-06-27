using PortfolioApi.DTOs;

namespace PortfolioApi.Interfaces;

/// <summary>
/// BUSINESS layer for the visitor counter: normalizes the country name and
/// orchestrates the increment. The controller stays a thin HTTP wrapper.
/// </summary>
public interface IVisitorService
{
    Task<long>                       GetTotalAsync();
    Task<long>                       HitAsync(string? country);  // increments → new total
    Task<IReadOnlyList<CountryCount>> GetCountriesAsync();
}
