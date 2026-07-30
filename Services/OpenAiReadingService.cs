using System.Text;
using System.Text.Json;
using PortfolioApi.Common;
using PortfolioApi.DTOs.Astrology;
using PortfolioApi.Interfaces;

namespace PortfolioApi.Services;

/// <summary>
/// Generates a personalised Vedic reading in Burmese from a summarised chart,
/// using an OpenAI-compatible chat-completions API. The base URL is configurable
/// (<c>AI:BaseUrl</c>), so any compatible provider works — OpenAI, Azure OpenAI,
/// OpenRouter, a local gateway, etc.
///
/// Config (environment variables shown in double-underscore form):
///   AI__OpenAiApiKey  — provider API key (required; endpoint 503s without it)
///   AI__Model         — model id, default "gpt-4o-mini"
///   AI__BaseUrl       — default "https://api.openai.com/v1"
/// </summary>
public class OpenAiReadingService : IAiReadingService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _cfg;
    private readonly ILogger<OpenAiReadingService> _log;

    public OpenAiReadingService(HttpClient http, IConfiguration cfg, ILogger<OpenAiReadingService> log)
    {
        _http = http;
        _cfg = cfg;
        _log = log;
    }

    // ── Prompt engineering: the astrologer persona + strict output contract ──────
    private const string SystemPrompt =
"""
You are an expert professional Vedic Astrologer who writes under the pen name
'ဆရာဘုန်းမင်းသိုက်ဒင်' (Saya Phone Myint Thaik Din). Your tone is majestic, deeply
empathetic, logically sound, and highly professional. You write ENTIRELY in elegant,
fluent, natural Burmese (မြန်မာ) — never mix in English sentences (technical
Sanskrit/Jyotish terms in Burmese transliteration are fine, e.g. ဒသာ, အန္တရ်ဒသာ,
အဋ္ဌကဝဂ်, ဆဒ္ဗလ). Always introduce yourself and sign the reading using ONLY this pen
name — STRICTLY never use or reveal the real name 'Myo Thant Naing' / 'မျိုးသန့်နိုင်'.

Use the astrological data provided by the user (planetary placements, current
Mahadasha / Antardasha / Pratyantardasha, Sade Sati status, Ashtakavarga scores,
and any yogas) to generate a personalised, coherent, and STRUCTURED life reading.

Hard rules:
1. Tie every prediction DIRECTLY to the provided mathematical data. When you make a
   statement, name the factor behind it (e.g. "လက်ရှိ စနေ ဒသာနှင့် စနေ၏ ၇ တန် တည်နေရာကြောင့် …").
   Never produce vague, generic, one-size-fits-all Barnum statements.
2. Be honest and humble. Astrology is interpretive guidance, not scientific fact — the
   CALCULATIONS are precise, but the outcomes are for reflection, not certainty. Never
   promise wealth/health/death dates with false confidence. Never induce fear.
3. Do NOT give definitive medical, legal, or financial directives. Offer reflective,
   constructive guidance and gentle, practical remedies (ဥပါယ်) only.
4. Output MUST be well-formed Markdown: use ## headings, **bold** for key terms, and
   - bullet lists. Keep it scannable and beautiful.

Structure your reading with these sections (translate the headings to Burmese):
  ## ✨ အနှစ်ချုပ် ခြုံငုံသုံးသပ်ချက်      (an overall summary anchored in Lagna + Moon)
  ## 🪐 ဂြိုဟ်တည်နေရာ အဓိကအချက်များ       (key placements & what they mean)
  ## ⏳ လက်ရှိ ဒသာကာလ ဟောကိန်း            (tie predictions to the current dasha window)
  ## 🎯 ဘဝကဏ္ဍအလိုက် ဟောကိန်း             (career, wealth, relationships, health, mind — use Ashtakavarga strength per sign)
  ## 🌑 Sade Sati / စိန်ခေါ်မှုကာလ        (only if Sade Sati is active or a hard transit is noted)
  ## 🙏 အကြံပြုချက်နှင့် ဥပါယ်            (practical, gentle remedies & reflections)

End with ONE short, warm sentence and a single humble line noting that this is guidance
for reflection, computed precisely but interpreted with care.
""";

    public async Task<ApiResponse<AiReadingResponseDto>> GenerateAsync(AiReadingRequestDto req, CancellationToken ct = default)
    {
        var apiKey = _cfg["AI:OpenAiApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _log.LogWarning("AI reading requested but AI:OpenAiApiKey is not configured.");
            return ApiResponse<AiReadingResponseDto>.Fail(
                "AI reading is not configured on the server yet.", 503);
        }

        var model = string.IsNullOrWhiteSpace(_cfg["AI:Model"]) ? "gpt-4o-mini" : _cfg["AI:Model"]!;
        var baseUrl = (string.IsNullOrWhiteSpace(_cfg["AI:BaseUrl"]) ? "https://api.openai.com/v1" : _cfg["AI:BaseUrl"]!).TrimEnd('/');
        var url = $"{baseUrl}/chat/completions";

        var userContent = BuildUserPrompt(req);

        var payload = new
        {
            model,
            messages = new object[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user", content = userContent },
            },
            temperature = 0.8,
            max_tokens = 2400,
        };

        using var msg = new HttpRequestMessage(HttpMethod.Post, url);
        msg.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");
        msg.Content = new StringContent(
            JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        try
        {
            using var resp = await _http.SendAsync(msg, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning("AI provider returned {Status}: {Body}", (int)resp.StatusCode, Truncate(body, 600));
                var friendly = (int)resp.StatusCode switch
                {
                    401 => "AI provider rejected the API key.",
                    429 => "AI provider is rate-limiting requests. Please try again shortly.",
                    _   => $"AI provider error ({(int)resp.StatusCode}).",
                };
                return ApiResponse<AiReadingResponseDto>.Fail(friendly, 502);
            }

            using var doc = JsonDocument.Parse(body);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(content))
                return ApiResponse<AiReadingResponseDto>.Fail("The AI returned an empty reading.", 502);

            return ApiResponse<AiReadingResponseDto>.Ok(new AiReadingResponseDto
            {
                Markdown = content.Trim(),
                Model = model,
                GeneratedAt = DateTime.UtcNow,
            }, "Reading generated.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return ApiResponse<AiReadingResponseDto>.Fail("The AI request timed out. Please try again.", 504);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "AI reading generation failed.");
            return ApiResponse<AiReadingResponseDto>.Fail("Could not reach the AI provider. Please try again later.", 502);
        }
    }

    /// <summary>Turn the summarised chart into a compact, clearly-labelled block the
    /// model can reason over deterministically.</summary>
    private static string BuildUserPrompt(AiReadingRequestDto r)
    {
        var sb = new StringBuilder();
        var lang = string.Equals(r.Language, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "my";
        sb.AppendLine(lang == "en"
            ? "Write the reading in English."
            : "Write the reading in Burmese (မြန်မာ).");
        sb.AppendLine();
        sb.AppendLine("=== CHART SNAPSHOT ===");

        if (!string.IsNullOrWhiteSpace(r.Name))   sb.AppendLine($"Querent: {r.Name}" + (string.IsNullOrWhiteSpace(r.Gender) ? "" : $" ({r.Gender})"));
        if (!string.IsNullOrWhiteSpace(r.NayNan)) sb.AppendLine($"Myanmar birth-day sign (နေ့နံ): {r.NayNan}");
        if (!string.IsNullOrWhiteSpace(r.Ascendant)) sb.AppendLine($"Ascendant (Lagna): {r.Ascendant}");
        if (!string.IsNullOrWhiteSpace(r.MoonSign))  sb.AppendLine($"Moon sign (Chandra Rasi): {r.MoonSign}");
        if (!string.IsNullOrWhiteSpace(r.SunSign))   sb.AppendLine($"Sun sign: {r.SunSign}");

        if (r.Placements.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Planetary placements:");
            foreach (var p in r.Placements)
            {
                var bits = new List<string> { $"House {p.House}", p.Sign };
                if (!string.IsNullOrWhiteSpace(p.Nakshatra)) bits.Add($"Nak. {p.Nakshatra}");
                if (!string.IsNullOrWhiteSpace(p.Dignity))   bits.Add(p.Dignity!);
                if (p.Retrograde) bits.Add("retrograde");
                sb.AppendLine($"  - {p.Planet}: {string.Join(", ", bits)}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("Current Vimshottari dasha:");
        sb.AppendLine($"  - Mahadasha: {Or(r.Mahadasha)}");
        sb.AppendLine($"  - Antardasha: {Or(r.Antardasha)}");
        sb.AppendLine($"  - Pratyantardasha: {Or(r.Pratyantardasha)}");
        if (!string.IsNullOrWhiteSpace(r.DashaWindow)) sb.AppendLine($"  - Window: {r.DashaWindow}");

        if (!string.IsNullOrWhiteSpace(r.SadeSatiStatus))
            sb.AppendLine($"\nSade Sati: {r.SadeSatiStatus}");

        if (r.SarvashtakavargaBySign is { Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine($"Sarvashtakavarga per sign (Aries→Pisces): {string.Join(", ", r.SarvashtakavargaBySign)}");
            if (!string.IsNullOrWhiteSpace(r.AshtakavargaNotes)) sb.AppendLine($"Ashtakavarga notes: {r.AshtakavargaNotes}");
        }

        if (r.Yogas is { Count: > 0 })
            sb.AppendLine($"\nActive yogas: {string.Join(", ", r.Yogas)}");

        if (r.FocusAreas is { Count: > 0 })
            sb.AppendLine($"\nPlease emphasise these life areas: {string.Join(", ", r.FocusAreas)}");

        if (!string.IsNullOrWhiteSpace(r.ExtraContext))
            sb.AppendLine($"\nAdditional context:\n{r.ExtraContext}");

        return sb.ToString();
    }

    private static string Or(string? s) => string.IsNullOrWhiteSpace(s) ? "(unknown)" : s;
    private static string Truncate(string s, int n) => string.IsNullOrEmpty(s) || s.Length <= n ? s : s[..n];
}
