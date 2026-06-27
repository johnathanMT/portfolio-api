using System.Security.Cryptography;
using System.Text;
using PortfolioApi.DTOs;
using PortfolioApi.Interfaces;
using PortfolioApi.Models;

namespace PortfolioApi.Services;

/// <summary>
/// Business rules for Sanctuary memories:
///   • hashes the raw operator token (SHA-256) so the raw id never touches the DB,
///   • masks each message unless the caller is its author or the Admin,
///   • sanitizes input, and
///   • enforces ONE memory per operator (edit-in-place).
/// Persistence is delegated to <see cref="IMemoryRepository"/>.
/// </summary>
public class MemoryService : IMemoryService
{
    private readonly IMemoryRepository _repo;
    public MemoryService(IMemoryRepository repo) => _repo = repo;

    private const string Masked = "🔒 Private Message";

    public async Task<IReadOnlyList<MemoryView>> GetMemoriesAsync(string? rawToken, bool isAdmin)
    {
        var callerHash = string.IsNullOrWhiteSpace(rawToken) ? null : Hash(rawToken);
        var all = await _repo.GetAllAsync();
        return all.Select(m =>
        {
            var mine = callerHash != null && m.OperatorToken == callerHash;
            // ── SERVER-SIDE PRIVACY MASKING ──
            var message = (isAdmin || mine) ? m.Message : Masked;
            return new MemoryView(m.Id, m.AuthorName, m.Landmark,
                m.PositionX, m.PositionY, m.PositionZ, m.CreatedAt, mine, message);
        }).ToList();
    }

    public async Task<MemoryWriteResult> SaveAsync(CreateMemoryDto dto, string? rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken) || rawToken.Length > 200)
            return new MemoryWriteResult(false, 0, false, "Missing or invalid operator token.");

        var author  = Sanitize(dto.Author, 40);
        var message = Sanitize(dto.Message, 240);
        if (author.Length == 0 || message.Length == 0)
            return new MemoryWriteResult(false, 0, false, "Name and message are required.");

        var tokenHash = Hash(rawToken);

        // ONE memory per operator: edit the existing one instead of adding another.
        var existing = await _repo.FindByOperatorAsync(tokenHash);
        if (existing is not null)
        {
            existing.AuthorName = author;
            existing.Message    = message;
            existing.Landmark   = dto.Landmark;
            existing.PositionX  = dto.PositionX;
            existing.PositionY  = dto.PositionY;
            existing.PositionZ  = dto.PositionZ;
            await _repo.UpdateAsync(existing);
            return new MemoryWriteResult(true, existing.Id, true, null);
        }

        var tag = new MemoryTag
        {
            AuthorName    = author,
            Message       = message,
            Landmark      = dto.Landmark,
            PositionX     = dto.PositionX,
            PositionY     = dto.PositionY,
            PositionZ     = dto.PositionZ,
            OperatorToken = tokenHash,
            CreatedAt     = DateTime.UtcNow,
        };
        await _repo.AddAsync(tag);
        return new MemoryWriteResult(true, tag.Id, false, null);
    }

    public async Task<IReadOnlyList<AdminMemoryView>> GetAllForAdminAsync()
    {
        var all = await _repo.GetAllAsync();
        return all.Select(m => new AdminMemoryView(
            m.Id, m.AuthorName, m.Message, m.Landmark,
            m.PositionX, m.PositionY, m.PositionZ, m.CreatedAt)).ToList();
    }

    private static string Hash(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw ?? string.Empty)));

    // Drop control characters + angle brackets → safe plain text; hard length cap.
    // NOT HTML-encoded: the client renders it as React text (auto-escaped), so
    // encoding here would surface literal entities like "it&#39;s".
    private static string Sanitize(string? s, int max)
    {
        s = (s ?? string.Empty).Trim();
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
            if (!char.IsControl(ch) && ch != '<' && ch != '>') sb.Append(ch);
        var clean = sb.ToString();
        return clean.Length > max ? clean[..max] : clean;
    }
}
