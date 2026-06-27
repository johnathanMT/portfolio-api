using Microsoft.EntityFrameworkCore;
using PortfolioApi.Data;
using PortfolioApi.Interfaces;
using PortfolioApi.Models;

namespace PortfolioApi.Repositories;

/// <summary>EF Core implementation of <see cref="IPoemRepository"/>.</summary>
public class PoemRepository : IPoemRepository
{
    private readonly AppDbContext _db;
    public PoemRepository(AppDbContext db) => _db = db;

    public async Task<IEnumerable<Poem>> GetAllAsync() =>
        await _db.Poems.AsNoTracking().OrderByDescending(p => p.CreatedDate).ToListAsync();

    public async Task<Poem?> GetByIdAsync(int id) =>
        await _db.Poems.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);

    public async Task<Poem> CreateAsync(Poem poem)
    {
        _db.Poems.Add(poem);
        await _db.SaveChangesAsync();
        return poem;
    }

    public async Task<Poem?> UpdateAsync(int id, Poem changes)
    {
        var poem = await _db.Poems.FirstOrDefaultAsync(p => p.Id == id);
        if (poem is null) return null;
        poem.Title    = changes.Title;
        poem.Subtitle = changes.Subtitle;
        poem.Content  = changes.Content;
        await _db.SaveChangesAsync();
        return poem;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var poem = await _db.Poems.FirstOrDefaultAsync(p => p.Id == id);
        if (poem is null) return false;
        _db.Poems.Remove(poem);
        await _db.SaveChangesAsync();
        return true;
    }
}
