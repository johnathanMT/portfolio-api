using Microsoft.EntityFrameworkCore;
using PortfolioApi.Data;
using PortfolioApi.Interfaces;
using PortfolioApi.Models;

namespace PortfolioApi.Repositories;

/// <summary>EF Core implementation of <see cref="IMemoryRepository"/>.</summary>
public class MemoryRepository : IMemoryRepository
{
    private readonly AppDbContext _db;
    public MemoryRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<MemoryTag>> GetAllAsync() =>
        await _db.MemoryTags.AsNoTracking()
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();

    public Task<MemoryTag?> FindByOperatorAsync(string operatorHash) =>
        _db.MemoryTags.FirstOrDefaultAsync(m => m.OperatorToken == operatorHash);

    public async Task AddAsync(MemoryTag tag)
    {
        _db.MemoryTags.Add(tag);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(MemoryTag tag)
    {
        // `tag` came from FindByOperatorAsync (tracked) and was mutated by the
        // service — just persist.
        await _db.SaveChangesAsync();
    }
}
