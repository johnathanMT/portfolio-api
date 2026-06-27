using System.Text;
using PortfolioApi.DTOs;
using PortfolioApi.Interfaces;
using PortfolioApi.Models;

namespace PortfolioApi.Services;

/// <summary>
/// Business rules for poems: sanitizes input and maps DTO → entity, then defers
/// persistence to <see cref="IPoemRepository"/>. No EF Core / HTTP here.
/// </summary>
public class PoemService : IPoemService
{
    private readonly IPoemRepository _repo;
    public PoemService(IPoemRepository repo) => _repo = repo;

    public Task<IEnumerable<Poem>> ListAsync() => _repo.GetAllAsync();
    public Task<Poem?> GetAsync(int id)        => _repo.GetByIdAsync(id);

    public Task<Poem> CreateAsync(PoemDto dto) => _repo.CreateAsync(ToEntity(dto));

    public Task<Poem?> UpdateAsync(int id, PoemDto dto) => _repo.UpdateAsync(id, ToEntity(dto));

    public Task<bool> DeleteAsync(int id) => _repo.DeleteAsync(id);

    // ── mapping + sanitization (the "business rules") ────────────────────────
    private static Poem ToEntity(PoemDto dto) => new()
    {
        Title       = Clean(dto.Title, 120),
        Subtitle    = Clean(dto.Subtitle, 80),
        Content     = CleanMultiline(dto.Content, 4000),
        CreatedDate = DateTime.UtcNow,
    };

    // Single-line: drop control chars + angle brackets, cap length.
    private static string Clean(string? s, int max)
    {
        s = (s ?? string.Empty).Trim();
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
            if (!char.IsControl(ch) && ch != '<' && ch != '>') sb.Append(ch);
        var clean = sb.ToString();
        return clean.Length > max ? clean[..max] : clean;
    }

    // Multi-line: KEEP newlines, drop other control chars + angle brackets.
    private static string CleanMultiline(string? s, int max)
    {
        s = (s ?? string.Empty).Replace("\r\n", "\n").Trim();
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
            if (ch == '\n' || (!char.IsControl(ch) && ch != '<' && ch != '>')) sb.Append(ch);
        var clean = sb.ToString();
        return clean.Length > max ? clean[..max] : clean;
    }
}
