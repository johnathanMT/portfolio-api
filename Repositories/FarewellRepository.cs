using Microsoft.EntityFrameworkCore;
using PortfolioApi.Data;
using PortfolioApi.Interfaces;
using PortfolioApi.Models;

namespace PortfolioApi.Repositories;

/// <summary>EF Core implementation of <see cref="IFarewellRepository"/>.</summary>
public class FarewellRepository : IFarewellRepository
{
    private readonly AppDbContext _db;
    public FarewellRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<FarewellRsvp>> GetAllAsync() =>
        await _db.FarewellRsvps.AsNoTracking()
            .OrderBy(f => f.CreatedAt)            // oldest first (plant order); admin re-sorts
            .ToListAsync();

    public Task<FarewellRsvp?> FindByOperatorAsync(string operatorHash) =>
        _db.FarewellRsvps.FirstOrDefaultAsync(f => f.OperatorToken == operatorHash);

    public Task<int> CountAsync() => _db.FarewellRsvps.CountAsync();

    public async Task AddAsync(FarewellRsvp rsvp)
    {
        _db.FarewellRsvps.Add(rsvp);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(FarewellRsvp rsvp)
    {
        // `rsvp` is tracked (from FindByOperatorAsync) and mutated by the service.
        await _db.SaveChangesAsync();
    }
}
