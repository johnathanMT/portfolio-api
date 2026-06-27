using System.Security.Cryptography;
using System.Text;
using PortfolioApi.DTOs;
using PortfolioApi.Interfaces;
using PortfolioApi.Models;

namespace PortfolioApi.Services;

/// <summary>
/// Business rules for farewell RSVPs:
///   • hashes the raw operator token (SHA-256),
///   • sanitizes input and clears dates/food when the visitor isn't attending,
///   • assigns a fixed RING plot around the main Sakura (so monuments never overlap),
///   • enforces ONE monument per visitor (edit-in-place, keeping the original plot).
/// Persistence is delegated to <see cref="IFarewellRepository"/>.
/// </summary>
public class FarewellService : IFarewellService
{
    private readonly IFarewellRepository _repo;
    public FarewellService(IFarewellRepository repo) => _repo = repo;

    // ── Memorial-grove ring layout (around the main Sakura) ───────────────────
    // The in-world Sakura sits at (x:0, z:0); the brief specified centre (0,-10).
    // Change CenterZ to 0f to hug the visible tree exactly.
    private const float CenterX = 0f;
    private const float CenterZ = -10f;
    private const int   PerRing = 8;
    private const float BaseRadius = 6f;
    private const float RingGap = 3f;

    private static (float x, float y, float z) PlotForIndex(int i)
    {
        int ring = i / PerRing;
        int slot = i % PerRing;
        float radius = BaseRadius + ring * RingGap;
        double angle = (2.0 * Math.PI / PerRing) * slot + (ring % 2 == 1 ? Math.PI / PerRing : 0.0);
        float x = CenterX + (float)(radius * Math.Cos(angle));
        float z = CenterZ + (float)(radius * Math.Sin(angle));
        return (x, 0f, z);
    }

    public async Task<IReadOnlyList<FarewellPlantView>> GetPlantsAsync(string? rawToken)
    {
        var callerHash = string.IsNullOrWhiteSpace(rawToken) ? null : Hash(rawToken);
        var all = await _repo.GetAllAsync();   // oldest first
        return all.Select(f => new FarewellPlantView(
            f.Id, f.Name, f.Message, f.PlantType,
            f.PositionX, f.PositionY, f.PositionZ, f.CreatedAt,
            callerHash != null && f.OperatorToken == callerHash)).ToList();
    }

    public async Task<FarewellWriteResult> SaveAsync(CreateFarewellRsvpDto dto, string? rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken) || rawToken.Length > 200)
            return Fail("Missing or invalid operator token.");

        var name    = Sanitize(dto.Name, 40);
        var message = Sanitize(dto.Message, 240);
        var plant   = (dto.PlantType ?? "sakura").Trim().ToLowerInvariant();
        var attending = dto.Attending;
        // If they can't join the party, dates/food are irrelevant → store empty.
        var dates = attending ? Sanitize(dto.DatesAvailable, 120) : string.Empty;
        var food  = attending ? Sanitize(dto.FoodPreference, 80)  : string.Empty;
        if (name.Length == 0 || message.Length == 0)
            return Fail("Name and message are required.");

        var tokenHash = Hash(rawToken);

        // ONE monument per visitor: edit in place, keep the original plot.
        var existing = await _repo.FindByOperatorAsync(tokenHash);
        if (existing is not null)
        {
            existing.Name           = name;
            existing.Message        = message;
            existing.Attending      = attending;
            existing.DatesAvailable = dates;
            existing.FoodPreference = food;
            existing.PlantType      = plant;
            await _repo.UpdateAsync(existing);
            return new FarewellWriteResult(true, existing.Id, existing.Name, existing.PlantType,
                existing.PositionX, existing.PositionY, existing.PositionZ, true, null);
        }

        // Assign the next free plot on the ring.
        var (px, py, pz) = PlotForIndex(await _repo.CountAsync());
        var rsvp = new FarewellRsvp
        {
            Name           = name,
            Message        = message,
            Attending      = attending,
            DatesAvailable = dates,
            FoodPreference = food,
            PlantType      = plant,
            PositionX      = px,
            PositionY      = py,
            PositionZ      = pz,
            OperatorToken  = tokenHash,
            CreatedAt      = DateTime.UtcNow,
        };
        await _repo.AddAsync(rsvp);
        return new FarewellWriteResult(true, rsvp.Id, rsvp.Name, rsvp.PlantType,
            rsvp.PositionX, rsvp.PositionY, rsvp.PositionZ, false, null);
    }

    public async Task<IReadOnlyList<FarewellRsvpAdminView>> GetAllForAdminAsync()
    {
        var all = await _repo.GetAllAsync();
        return all.OrderByDescending(f => f.CreatedAt)   // newest first for the admin table
            .Select(f => new FarewellRsvpAdminView(
                f.Id, f.Name, f.Message, f.Attending,
                f.DatesAvailable, f.FoodPreference, f.PlantType,
                f.PositionX, f.PositionY, f.PositionZ, f.CreatedAt)).ToList();
    }

    private static FarewellWriteResult Fail(string error) =>
        new(false, 0, string.Empty, string.Empty, 0, 0, 0, false, error);

    private static string Hash(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw ?? string.Empty)));

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
