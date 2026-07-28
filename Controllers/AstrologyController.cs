using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PortfolioApi.Common;
using PortfolioApi.Data;
using PortfolioApi.DTOs.Astrology;
using PortfolioApi.Interfaces;
using PortfolioApi.Models;
using PortfolioApi.Security;

namespace PortfolioApi.Controllers;

/// <summary>
/// Vedic astrology — computes a sidereal Rasi (D1) birth chart from birth details.
/// Public, stateless, rate-limited. POST /api/astrology/chart.
/// </summary>
[ApiController]
[Route("api/astrology")]
[Produces("application/json")]
public class AstrologyController : ControllerBase
{
    private readonly IAstrologyService _service;
    private readonly AppDbContext _db;
    private readonly IEmailService _email;
    private readonly IConfiguration _cfg;
    private readonly string _encKey;

    public AstrologyController(IAstrologyService service, AppDbContext db, IEmailService email, IConfiguration cfg)
    {
        _service = service;
        _db = db;
        _email = email;
        _cfg = cfg;
        // Dedicated key preferred; falls back to the JWT key so it works out of the box.
        _encKey = cfg["Astrology:EncryptionKey"] ?? cfg["Jwt:Key"] ?? "astrology-fallback-key-set-in-env";
    }

    /// <summary>Compute a sidereal Rasi (D1) chart.</summary>
    /// <remarks>
    ///     POST /api/astrology/chart
    ///     { "year":1998, "month":1, "day":1, "hour":12, "minute":0, "second":0,
    ///       "timeZone":"Asia/Yangon", "latitude":16.8409, "longitude":96.1735 }
    /// </remarks>
    [HttpPost("chart")]
    [EnableRateLimiting("astrology")]
    [ProducesResponseType(typeof(ApiResponse<BirthChartData>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public IActionResult Chart([FromBody] BirthChartRequest req)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Fail(
                "Validation failed.", 400,
                ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));

        var result = _service.ComputeRasiChart(req);
        return result.StatusCode switch
        {
            200 => Ok(result),
            400 => BadRequest(result),
            _   => StatusCode(result.StatusCode, result),
        };
    }

    // ── Remedy (yatra) / contact request — public, stored encrypted ──────────────
    [HttpPost("remedy-request")]
    [EnableRateLimiting("general")]
    public async Task<IActionResult> RemedyRequest([FromBody] RemedyRequestDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Fail("Validation failed.", 400));

        var row = new RemedyRequest
        {
            Name = FieldCrypto.Encrypt(dto.Name, _encKey),
            Contact = FieldCrypto.Encrypt(dto.Contact, _encKey),
            Area = dto.Area,
            Message = FieldCrypto.Encrypt(dto.Message, _encKey),
            BirthInfo = FieldCrypto.Encrypt($"{dto.BirthDate} {dto.BirthTime}".Trim(), _encKey),
            Handled = false,
            CreatedAt = DateTime.UtcNow,
        };
        _db.RemedyRequests.Add(row);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { row.Id }, "Remedy request received."));
    }

    // ── Opt-in chart save — public, stored encrypted only WITH consent ──────────
    [HttpPost("save-chart")]
    [EnableRateLimiting("astrology")]
    public async Task<IActionResult> SaveChart([FromBody] SaveChartDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Fail("Validation failed.", 400));
        if (!dto.Consent)
            return Ok(ApiResponse<object>.Ok(new { stored = false }, "No consent — not stored."));

        var row = new QuerentChart
        {
            Name = FieldCrypto.Encrypt(dto.Name, _encKey),
            Gender = dto.Gender,
            BirthDate = FieldCrypto.Encrypt(dto.BirthDate, _encKey),
            BirthTime = FieldCrypto.Encrypt(dto.BirthTime, _encKey),
            TimeZone = dto.TimeZone,
            Location = FieldCrypto.Encrypt($"{dto.Latitude},{dto.Longitude}", _encKey),
            NayNan = dto.NayNan,
            Consent = true,
            CreatedAt = DateTime.UtcNow,
        };
        _db.QuerentCharts.Add(row);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { row.Id, stored = true }, "Saved."));
    }

    // ── Admin: remedy requests (decrypted) ──────────────────────────────────────
    [HttpGet("admin/remedies")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> AdminRemedies()
    {
        var rows = await _db.RemedyRequests.OrderByDescending(r => r.CreatedAt).Take(500).ToListAsync();
        var view = rows.Select(r => new RemedyView
        {
            Id = r.Id,
            Name = FieldCrypto.Decrypt(r.Name, _encKey),
            Contact = FieldCrypto.Decrypt(r.Contact, _encKey),
            Area = r.Area,
            Message = FieldCrypto.Decrypt(r.Message, _encKey),
            BirthInfo = FieldCrypto.Decrypt(r.BirthInfo, _encKey),
            Handled = r.Handled,
            CreatedAt = r.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
        }).ToList();
        return Ok(ApiResponse<List<RemedyView>>.Ok(view, "OK"));
    }

    // ── Admin: toggle handled ───────────────────────────────────────────────────
    [HttpPatch("admin/remedies/{id:int}/handled")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> ToggleHandled(int id)
    {
        var row = await _db.RemedyRequests.FindAsync(id);
        if (row is null) return NotFound(ApiResponse<object>.Fail("Not found.", 404));
        row.Handled = !row.Handled;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { row.Id, row.Handled }, "Updated."));
    }

    // ── Admin: saved querent charts (decrypted) ─────────────────────────────────
    [HttpGet("admin/charts")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> AdminCharts()
    {
        var rows = await _db.QuerentCharts.OrderByDescending(c => c.CreatedAt).Take(500).ToListAsync();
        var view = rows.Select(c => new QuerentChartView
        {
            Id = c.Id,
            Name = FieldCrypto.Decrypt(c.Name, _encKey),
            Gender = c.Gender,
            BirthDate = FieldCrypto.Decrypt(c.BirthDate, _encKey),
            BirthTime = FieldCrypto.Decrypt(c.BirthTime, _encKey),
            TimeZone = c.TimeZone,
            Location = FieldCrypto.Decrypt(c.Location, _encKey),
            NayNan = c.NayNan,
            CreatedAt = c.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
        }).ToList();
        return Ok(ApiResponse<List<QuerentChartView>>.Ok(view, "OK"));
    }

    // ── PDF request (public) — stored Pending, encrypted ────────────────────────
    [HttpPost("request-pdf")]
    [EnableRateLimiting("general")]
    public async Task<IActionResult> RequestPdf([FromBody] RequestPdfDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Fail("Validation failed.", 400));
        var row = new PdfRequest
        {
            Email = FieldCrypto.Encrypt(dto.Email, _encKey),
            Name = FieldCrypto.Encrypt(dto.Name, _encKey),
            BirthInfo = FieldCrypto.Encrypt($"{dto.BirthDate} {dto.BirthTime}".Trim(), _encKey),
            ApprovalStatus = "Pending",
            CreatedAt = DateTime.UtcNow,
        };
        _db.PdfRequests.Add(row);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { row.Id }, "PDF request received — awaiting admin approval."));
    }

    // ── Admin: list PDF requests ────────────────────────────────────────────────
    [HttpGet("admin/pdf-requests")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> AdminPdfRequests()
    {
        var rows = await _db.PdfRequests.OrderByDescending(r => r.CreatedAt).Take(500).ToListAsync();
        var view = rows.Select(r => new PdfRequestView
        {
            Id = r.Id,
            Email = FieldCrypto.Decrypt(r.Email, _encKey),
            Name = FieldCrypto.Decrypt(r.Name, _encKey),
            BirthInfo = FieldCrypto.Decrypt(r.BirthInfo, _encKey),
            ApprovalStatus = r.ApprovalStatus,
            CreatedAt = r.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
        }).ToList();
        return Ok(ApiResponse<List<PdfRequestView>>.Ok(view, "OK"));
    }

    // ── Admin: approve + email a secure one-time link (48h) ─────────────────────
    [HttpPost("approve-pdf/{id:int}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> ApprovePdf(int id)
    {
        var row = await _db.PdfRequests.FindAsync(id);
        if (row is null) return NotFound(ApiResponse<object>.Fail("Not found.", 404));

        string token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        row.DownloadToken = token;
        row.TokenExpiry = DateTime.UtcNow.AddHours(48);
        row.ApprovalStatus = "Approved";
        await _db.SaveChangesAsync();

        string baseUrl = (_cfg["App:PdfDownloadBase"] ?? "https://myweb-zqv1.onrender.com/api/astrology/download-pdf").TrimEnd('/');
        string link = $"{baseUrl}?token={token}";
        string email = FieldCrypto.Decrypt(row.Email, _encKey);
        bool sent = await _email.SendAsync(email, "သင်၏ ဗေဒင်ဟောစာတမ်း (PDF) — Vedin", PdfApprovedEmail(link));

        return Ok(ApiResponse<object>.Ok(new { row.Id, row.ApprovalStatus, emailSent = sent }, sent ? "Approved & emailed." : "Approved (SMTP not configured — set Smtp__* env vars)."));
    }

    // ── Public: secure one-time PDF download ────────────────────────────────────
    [HttpGet("download-pdf")]
    [EnableRateLimiting("general")]
    public async Task<IActionResult> DownloadPdf([FromQuery] string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return BadRequest("Missing token.");
        var row = await _db.PdfRequests.FirstOrDefaultAsync(r => r.DownloadToken == token);
        if (row is null || row.ApprovalStatus != "Approved" || row.TokenExpiry is null || row.TokenExpiry < DateTime.UtcNow)
            return StatusCode(410, "This link is invalid, already used, or expired.");

        string name = FieldCrypto.Decrypt(row.Name, _encKey);
        string birth = FieldCrypto.Decrypt(row.BirthInfo, _encKey);
        var pdf = MiniPdf.Build("Vedin - Vedic Astrology Reading", new[]
        {
            "Sayar Myo Thant Naing - Professional Vedic Astrology",
            "",
            string.IsNullOrWhiteSpace(name) ? "Reading for: (querent)" : $"Reading for: {name}",
            string.IsNullOrWhiteSpace(birth) ? "" : $"Birth: {birth}",
            "",
            "Thank you for your request. Your reading document has been",
            "securely approved and delivered via this one-time link.",
            "",
            "(Placeholder PDF — the full encyclopedia layout is generated",
            " from your computed chart.)",
        });

        row.ApprovalStatus = "Downloaded";   // one-time: invalidate the link
        row.DownloadToken = string.Empty;
        await _db.SaveChangesAsync();

        return File(pdf, "application/pdf", "vedin-reading.pdf");
    }

    // Premium branded HTML email (purple / gold).
    private static string PdfApprovedEmail(string link)
    {
        const string tpl = """
<!doctype html><html><body style="margin:0;background:#0b0a14;font-family:Segoe UI,Helvetica,Arial,sans-serif">
  <div style="max-width:560px;margin:0 auto;padding:32px 20px">
    <div style="background:linear-gradient(135deg,#14121f,#1b1830);border:1px solid rgba(168,85,247,.35);border-radius:18px;padding:34px 28px;box-shadow:0 0 60px -20px rgba(168,85,247,.5)">
      <div style="font:600 12px 'Segoe UI';letter-spacing:.3em;text-transform:uppercase;color:#eab308;margin-bottom:14px">Vedin &middot; Vedic Astrology</div>
      <h1 style="margin:0 0 8px;font-size:22px;color:#f2ede0">Sayar Myo Thant Naing</h1>
      <p style="margin:0 0 22px;color:#b9b09b;font-size:14px;line-height:1.9">ဂုဏ်ယူပါသည်။ သင်၏ ဗေဒင်ဟောစာတမ်း (PDF) ကို Admin မှ အတည်ပြုပေးလိုက်ပါပြီ။ အောက်ပါလင့်ခ်မှတစ်ဆင့် လုံခြုံစွာ ရယူနိုင်ပါသည်။</p>
      <a href="{{LINK}}" style="display:inline-block;background:linear-gradient(135deg,#a855f7,#eab308);color:#14110d;font-weight:700;text-decoration:none;padding:14px 26px;border-radius:12px;font-size:15px">Download your reading (PDF)</a>
      <p style="margin:22px 0 0;color:#726a5c;font-size:12px;line-height:1.8">This secure link works once and expires in 48 hours. If you didn't request this, please ignore this email.</p>
    </div>
    <p style="text-align:center;color:#4a443b;font-size:11px;margin-top:16px">Vedin &middot; myothant.dev</p>
  </div>
</body></html>
""";
        return tpl.Replace("{{LINK}}", link);
    }
}
