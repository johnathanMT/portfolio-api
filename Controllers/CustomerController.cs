using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using PortfolioApi.Common;
using PortfolioApi.Data;
using PortfolioApi.DTOs.Astrology;
using PortfolioApi.DTOs.Auth;
using PortfolioApi.Interfaces;
using PortfolioApi.Models;
using PortfolioApi.Security;
using PortfolioApi.Services;

namespace PortfolioApi.Controllers;

/// <summary>
/// Querent (customer) accounts — email-only sign-up with email confirmation,
/// login (JWT, role "Customer"), profile (me) and editable username. Reuses the
/// same JWT signing key as admin auth, so the existing validation middleware
/// accepts customer tokens; customers never receive the Admin role.
/// </summary>
[ApiController]
[Route("api/customer")]
[Produces("application/json")]
public class CustomerController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IEmailService _email;
    private readonly IConfiguration _cfg;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CustomerController> _log;
    private readonly string _encKey;

    public CustomerController(AppDbContext db, IEmailService email, IConfiguration cfg, IMemoryCache cache, ILogger<CustomerController> log)
    {
        _db = db;
        _email = email;
        _cfg = cfg;
        _cache = cache;
        _log = log;
        _encKey = cfg["Astrology:EncryptionKey"] ?? cfg["Jwt:Key"] ?? "astrology-fallback-key-set-in-env";
    }

    // ── Resend confirmation (anti-spam: 60s cooldown + 3/hour, anti-enumeration) ─
    [HttpPost("resend-confirmation")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResendConfirmation([FromBody] ResendDto dto)
    {
        const string generic = "If an unverified account exists with this email, a confirmation link has been sent.";
        var email = (dto.Email ?? string.Empty).ToLowerInvariant().Trim();
        string ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        int Count(string key) => _cache.TryGetValue(key, out int c) ? c : 0;
        void Bump(string key) => _cache.Set(key, Count(key) + 1, TimeSpan.FromHours(1));

        bool valid = email.Length is > 3 and < 200 && email.Contains('@');
        bool throttled =
            _cache.TryGetValue($"rc:cool:e:{email}", out _) ||
            _cache.TryGetValue($"rc:cool:i:{ip}", out _) ||
            Count($"rc:cnt:e:{email}") >= 3 ||
            Count($"rc:cnt:i:{ip}") >= 3;

        if (valid && !throttled)
        {
            var cust = await _db.Customers.FirstOrDefaultAsync(c => c.Email == email);
            if (cust is not null && !cust.EmailConfirmed)
            {
                string token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");   // 64 hex chars
                cust.VerifyToken = token;                                                       // invalidates the old one
                cust.VerifyExpiry = DateTime.UtcNow.AddHours(48);
                cust.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                string apiBase = (_cfg["App:ApiBase"] ?? "https://myweb-zqv1.onrender.com").TrimEnd('/');
                await _email.SendAsync(email, "Vedin — သင့်အကောင့်ကို အတည်ပြုပါ", VerifyEmailHtml($"{apiBase}/api/customer/verify-email?token={token}"));
            }
            // Apply throttle counters whether or not the account exists (constant behaviour).
            _cache.Set($"rc:cool:e:{email}", true, TimeSpan.FromSeconds(60));
            _cache.Set($"rc:cool:i:{ip}", true, TimeSpan.FromSeconds(60));
            Bump($"rc:cnt:e:{email}");
            Bump($"rc:cnt:i:{ip}");
        }

        return Ok(ApiResponse<object>.Ok(new { }, generic));
    }

    // ── Sign up (email only) + send confirmation email ──────────────────────────
    [HttpPost("signup")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Signup([FromBody] CustomerSignupDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse<object>.Fail("Validation failed.", 400));
        if (dto.Password != dto.ConfirmPassword)
            return BadRequest(ApiResponse<object>.Fail("Passwords do not match.", 400));

        var email = dto.Email.ToLowerInvariant().Trim();
        if (await _db.Customers.AnyAsync(c => c.Email == email))
            return Conflict(ApiResponse<object>.Fail("This email is already registered.", 409));

        string token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var cust = new Customer
        {
            Email = email,
            Username = dto.Username.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password, workFactor: 12),
            EmailConfirmed = false,
            VerifyToken = token,
            VerifyExpiry = DateTime.UtcNow.AddHours(48),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.Customers.Add(cust);
        await _db.SaveChangesAsync();

        string apiBase = (_cfg["App:ApiBase"] ?? "https://myweb-zqv1.onrender.com").TrimEnd('/');
        string link = $"{apiBase}/api/customer/verify-email?token={token}";

        // Sending is the step that fails when SMTP is misconfigured. Wrap it so a
        // provider exception surfaces as a clear 400 instead of a raw 500. The
        // account row already exists, so the querent can retry via resend-confirmation.
        bool sent;
        try
        {
            sent = await _email.SendAsync(email, "Vedin — သင့်အကောင့်ကို အတည်ပြုပါ", VerifyEmailHtml(link));
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Confirmation email failed to send for {Email}.", email);
            return BadRequest(ApiResponse<object>.Fail("Failed to send confirmation email. Please check server SMTP settings.", 400));
        }
        if (!sent)
            return BadRequest(ApiResponse<object>.Fail("Failed to send confirmation email. Please check server SMTP settings.", 400));

        return Ok(ApiResponse<object>.Ok(new { emailSent = true },
            "Account created — please check your email to confirm your address."));
    }

    // ── Confirm email (link target) — returns a small HTML page ─────────────────
    [HttpGet("verify-email")]
    [EnableRateLimiting("general")]
    public async Task<IActionResult> VerifyEmail([FromQuery] string token)
    {
        var cust = string.IsNullOrWhiteSpace(token) ? null
            : await _db.Customers.FirstOrDefaultAsync(c => c.VerifyToken == token);
        if (cust is null || cust.VerifyExpiry is null || cust.VerifyExpiry < DateTime.UtcNow)
            return base.Content(VerifyPageHtml(false), "text/html");

        cust.EmailConfirmed = true;
        cust.VerifyToken = null;
        cust.VerifyExpiry = null;
        cust.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Auto-login: mint a JWT and bounce back to the Vedin page with it, so the
        // front-end can log the querent in automatically (no manual sign-in needed).
        var loginToken = GenerateJwt(cust);
        var frontendUrl = (_cfg["Frontend:Url"] ?? "https://myothant.dev").TrimEnd('/');
        return Redirect($"{frontendUrl}/jyotish?verified=true&token={Uri.EscapeDataString(loginToken)}");
    }

    // ── Login (only after email confirmed) ──────────────────────────────────────
    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] CustomerLoginDto dto)
    {
        var email = dto.Email.ToLowerInvariant().Trim();
        var cust = await _db.Customers.FirstOrDefaultAsync(c => c.Email == email);
        if (cust is null || !BCrypt.Net.BCrypt.Verify(dto.Password, cust.PasswordHash))
            return Unauthorized(ApiResponse<object>.Fail("Invalid email or password.", 401));
        if (!cust.EmailConfirmed)
            return Unauthorized(ApiResponse<object>.Fail("Please confirm your email before signing in.", 401));

        var token = GenerateJwt(cust);
        return Ok(ApiResponse<object>.Ok(new { token, cust.Id, cust.Email, cust.Username }, "Login successful."));
    }

    // ── Me (authenticated customer) ─────────────────────────────────────────────
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        if (!TryCustomerId(out int id)) return Unauthorized(ApiResponse<object>.Fail("Not a customer token.", 401));
        var cust = await _db.Customers.FindAsync(id);
        if (cust is null) return NotFound(ApiResponse<object>.Fail("Account not found.", 404));
        return Ok(ApiResponse<object>.Ok(new { cust.Id, cust.Email, cust.Username, cust.EmailConfirmed }, "OK"));
    }

    // ── Update own username ─────────────────────────────────────────────────────
    [HttpPatch("username")]
    [Authorize]
    public async Task<IActionResult> UpdateUsername([FromBody] UpdateUsernameDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse<object>.Fail("Validation failed.", 400));
        if (!TryCustomerId(out int id)) return Unauthorized(ApiResponse<object>.Fail("Not a customer token.", 401));
        var cust = await _db.Customers.FindAsync(id);
        if (cust is null) return NotFound(ApiResponse<object>.Fail("Account not found.", 404));
        cust.Username = dto.Username.Trim();
        cust.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { cust.Username }, "Username updated."));
    }

    // ── Save a chart under the account ──────────────────────────────────────────
    [HttpPost("save-chart")]
    [Authorize]
    public async Task<IActionResult> SaveChart([FromBody] SaveChartDto dto)
    {
        if (!TryCustomerId(out int id)) return Unauthorized(ApiResponse<object>.Fail("Not a customer token.", 401));

        var name = (dto.Name ?? string.Empty).Trim();

        // ── Deduplication / upsert ──────────────────────────────────────────────
        // Name / BirthDate / BirthTime are AES-GCM encrypted with a RANDOM nonce, so
        // the same plaintext yields different ciphertext every time — we cannot match
        // on the encrypted columns in SQL. Instead we decrypt this customer's charts
        // (capped at 100) and compare the plaintext. On a match we UPDATE that row
        // rather than inserting a duplicate.
        var mine = await _db.CustomerCharts.Where(c => c.CustomerId == id).ToListAsync();
        string Dec(string cipher) { try { return FieldCrypto.Decrypt(cipher, _encKey); } catch { return string.Empty; } }
        var existing = mine.FirstOrDefault(c =>
            string.Equals(Dec(c.Name).Trim(), name, StringComparison.OrdinalIgnoreCase)
            && Dec(c.BirthDate) == dto.BirthDate
            && Dec(c.BirthTime) == dto.BirthTime);

        if (existing is not null)
        {
            // Refresh the mutable fields + timestamp; no duplicate row is created.
            existing.Gender = dto.Gender;
            existing.TimeZone = dto.TimeZone;
            existing.Location = FieldCrypto.Encrypt($"{dto.Latitude},{dto.Longitude}", _encKey);
            existing.NayNan = dto.NayNan;
            existing.CreatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return Ok(ApiResponse<object>.Ok(new { existing.Id, deduped = true }, "Chart already saved — updated."));
        }

        var row = new CustomerChart
        {
            CustomerId = id,
            Name = FieldCrypto.Encrypt(name, _encKey),
            Gender = dto.Gender,
            BirthDate = FieldCrypto.Encrypt(dto.BirthDate, _encKey),
            BirthTime = FieldCrypto.Encrypt(dto.BirthTime, _encKey),
            TimeZone = dto.TimeZone,
            Location = FieldCrypto.Encrypt($"{dto.Latitude},{dto.Longitude}", _encKey),
            NayNan = dto.NayNan,
            CreatedAt = DateTime.UtcNow,
        };
        _db.CustomerCharts.Add(row);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { row.Id, deduped = false }, "Chart saved to your account."));
    }

    // ── List the account's saved charts (decrypted) → form autofill ─────────────
    [HttpGet("my-charts")]
    [Authorize]
    public async Task<IActionResult> MyCharts()
    {
        if (!TryCustomerId(out int id)) return Unauthorized(ApiResponse<object>.Fail("Not a customer token.", 401));
        var rows = await _db.CustomerCharts.Where(c => c.CustomerId == id).OrderByDescending(c => c.CreatedAt).Take(100).ToListAsync();
        var view = rows.Select(c =>
        {
            var loc = FieldCrypto.Decrypt(c.Location, _encKey).Split(',');
            double.TryParse(loc.ElementAtOrDefault(0), out var lat);
            double.TryParse(loc.ElementAtOrDefault(1), out var lon);
            return new CustomerChartView
            {
                Id = c.Id,
                Name = FieldCrypto.Decrypt(c.Name, _encKey),
                Gender = c.Gender,
                BirthDate = FieldCrypto.Decrypt(c.BirthDate, _encKey),
                BirthTime = FieldCrypto.Decrypt(c.BirthTime, _encKey),
                TimeZone = c.TimeZone,
                Latitude = lat,
                Longitude = lon,
                NayNan = c.NayNan,
                CreatedAt = c.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
            };
        }).ToList();
        return Ok(ApiResponse<List<CustomerChartView>>.Ok(view, "OK"));
    }

    // ── Admin: list ALL customers' saved charts (decrypted) — for cleanup ───────
    [HttpGet("admin/saved-charts")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> AdminSavedCharts()
    {
        try
        {
            var rows = await _db.CustomerCharts.OrderByDescending(c => c.CreatedAt).Take(1000).ToListAsync();
            string Dec(string? s) { if (string.IsNullOrEmpty(s)) return string.Empty; try { return FieldCrypto.Decrypt(s, _encKey); } catch { return "[decrypt-error]"; } }
            var view = rows.Select(c => new QuerentChartView
            {
                Id = c.Id,
                Name = Dec(c.Name),
                Gender = c.Gender,
                BirthDate = Dec(c.BirthDate),
                BirthTime = Dec(c.BirthTime),
                TimeZone = c.TimeZone,
                Location = Dec(c.Location),
                NayNan = c.NayNan,
                CreatedAt = c.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
            }).ToList();
            return Ok(ApiResponse<List<QuerentChartView>>.Ok(view, "OK"));
        }
        catch (Exception)
        {
            return Ok(ApiResponse<List<QuerentChartView>>.Ok(new List<QuerentChartView>(), "OK"));
        }
    }

    // ── Admin: delete one customer saved chart (clear duplicate records) ────────
    [HttpDelete("admin/saved-charts/{id:int}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> AdminDeleteSavedChart(int id)
    {
        var row = await _db.CustomerCharts.FindAsync(id);
        if (row is null) return NotFound(ApiResponse<object>.Fail("Not found.", 404));
        _db.CustomerCharts.Remove(row);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { id }, "Deleted."));
    }

    // ── Account-based PDF download (no admin approval, no SMTP) ──────────────────
    [HttpGet("download-pdf")]
    [Authorize]
    public async Task<IActionResult> DownloadPdf([FromQuery] int? chartId)
    {
        if (!TryCustomerId(out int id)) return Unauthorized(ApiResponse<object>.Fail("Not a customer token.", 401));
        var q = _db.CustomerCharts.Where(c => c.CustomerId == id);
        var chart = chartId is int cid
            ? await q.FirstOrDefaultAsync(c => c.Id == cid)
            : await q.OrderByDescending(c => c.CreatedAt).FirstOrDefaultAsync();
        if (chart is null) return NotFound(ApiResponse<object>.Fail("No saved chart to export.", 404));

        string name = FieldCrypto.Decrypt(chart.Name, _encKey);
        string bd = FieldCrypto.Decrypt(chart.BirthDate, _encKey);
        string bt = FieldCrypto.Decrypt(chart.BirthTime, _encKey);
        var pdf = MiniPdf.Build("Vedin - Vedic Astrology Reading", new[]
        {
            "Sayar Bhone Min Thike Din - Professional Vedic Astrology", "",
            string.IsNullOrWhiteSpace(name) ? "Reading for: (you)" : $"Reading for: {name}",
            $"Birth: {bd} {bt}".Trim(), "",
            "Your reading document, generated from your saved chart.",
        });
        return File(pdf, "application/pdf", "vedin-reading.pdf");
    }

    // ── helpers ─────────────────────────────────────────────────────────────────
    private bool TryCustomerId(out int id)
    {
        id = 0;
        if (User.FindFirst("ctype")?.Value != "customer") return false;
        return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out id);
    }

    private string GenerateJwt(Customer c)
    {
        var jwtKey = _cfg["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured.");
        var issuer = _cfg["Jwt:Issuer"] ?? "PortfolioApi";
        var audience = _cfg["Jwt:Audience"] ?? "PortfolioApiUsers";
        int expHours = int.TryParse(_cfg["Jwt:ExpirationHours"], out var h) ? h : 24;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, c.Id.ToString()),
            new(ClaimTypes.Email, c.Email),
            new(ClaimTypes.Name, c.Username),
            new(ClaimTypes.Role, "Customer"),
            new("ctype", "customer"),
            new(JwtRegisteredClaimNames.Sub, c.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        var token = new JwtSecurityToken(issuer, audience, claims, DateTime.UtcNow, DateTime.UtcNow.AddHours(expHours), creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private string VerifyEmailHtml(string link)
    {
        const string tpl = """
<!doctype html><html><body style="margin:0;background:#0b0a14;font-family:Segoe UI,Helvetica,Arial,sans-serif">
  <div style="max-width:560px;margin:0 auto;padding:32px 20px">
    <div style="background:linear-gradient(135deg,#14121f,#1b1830);border:1px solid rgba(168,85,247,.35);border-radius:18px;padding:34px 28px;box-shadow:0 0 60px -20px rgba(168,85,247,.5)">
      <div style="font:600 12px 'Segoe UI';letter-spacing:.3em;text-transform:uppercase;color:#eab308;margin-bottom:14px">Vedin &middot; Vedic Astrology</div>
      <h1 style="margin:0 0 8px;font-size:22px;color:#f2ede0">Confirm your email</h1>
      <p style="margin:0 0 22px;color:#b9b09b;font-size:14px;line-height:1.9">Vedin အကောင့် ဖန်တီးသည့်အတွက် ကျေးဇူးတင်ပါသည်။ အောက်ပါခလုတ်ကို နှိပ်၍ သင့်အီးမေးလ်ကို အတည်ပြုပါ။ ထို့နောက် အကောင့်ဝင်၍ ဗေဒင်ဟောစာတမ်းများ ရယူနိုင်ပါသည်။</p>
      <a href="{{LINK}}" style="display:inline-block;background:linear-gradient(135deg,#a855f7,#eab308);color:#14110d;font-weight:700;text-decoration:none;padding:14px 26px;border-radius:12px;font-size:15px">Confirm my email</a>
      <p style="margin:22px 0 0;color:#726a5c;font-size:12px;line-height:1.8">This link expires in 48 hours. If you didn't create a Vedin account, you can ignore this email.</p>
    </div>
    <p style="text-align:center;color:#4a443b;font-size:11px;margin-top:16px">Vedin &middot; myothant.dev</p>
  </div>
</body></html>
""";
        return tpl.Replace("{{LINK}}", link);
    }

    private static string VerifyPageHtml(bool ok) => ok
        ? "<!doctype html><meta charset=\"utf-8\"><body style=\"margin:0;background:#0b0a14;color:#e8e3d6;font-family:Segoe UI,Arial,sans-serif;display:flex;min-height:100vh;align-items:center;justify-content:center\"><div style=\"text-align:center;padding:40px\"><div style=\"font-size:52px\">&#10003;</div><h1 style=\"color:#eab308\">Email confirmed</h1><p style=\"color:#b9b09b\">Your Vedin account is verified. You can now sign in and view your readings.</p><a href=\"https://www.myothant.dev/jyotish\" style=\"color:#a855f7\">Go to Vedin &rarr;</a></div></body>"
        : "<!doctype html><meta charset=\"utf-8\"><body style=\"margin:0;background:#0b0a14;color:#e8e3d6;font-family:Segoe UI,Arial,sans-serif;display:flex;min-height:100vh;align-items:center;justify-content:center\"><div style=\"text-align:center;padding:40px\"><h1 style=\"color:#fb4158\">Link invalid or expired</h1><p style=\"color:#b9b09b\">This confirmation link is no longer valid. Please sign up again or request a new link.</p></div></body>";
}
