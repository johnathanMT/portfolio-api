using PortfolioApi.DTOs;
using PortfolioApi.Interfaces;

namespace PortfolioApi.Services;

/// <summary>
/// Business rules for the visitor counter: trims/caps the country name, then
/// orchestrates the repository (increment total, optionally a country, read total).
/// </summary>
public class VisitorService : IVisitorService
{
    private readonly IVisitorRepository _repo;
    public VisitorService(IVisitorRepository repo) => _repo = repo;

    public Task<long> GetTotalAsync() => _repo.GetTotalAsync();

    public async Task<long> HitAsync(string? country)
    {
        await _repo.IncrementTotalAsync();

        var name = (country ?? string.Empty).Trim();
        if (name.Length > 100) name = name[..100];
        if (name.Length > 0) await _repo.IncrementCountryAsync(name);

        return await _repo.GetTotalAsync();
    }

    public Task<IReadOnlyList<CountryCount>> GetCountriesAsync() => _repo.GetCountriesAsync();
}
