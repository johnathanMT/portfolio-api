using System.Text;
using System.Text.Json;
using PortfolioApi.Common;
using PortfolioApi.DTOs.Astrology;
using PortfolioApi.Interfaces;

namespace PortfolioApi.Services;

/// <summary>
/// Generates a personalised Vedic reading in Burmese from a summarised chart,
/// using Google's Gemini API (generativelanguage.googleapis.com). Uses the native
/// <c>:generateContent</c> endpoint with a <c>systemInstruction</c>, so the
/// astrologer persona is enforced server-side and never travels in the user turn.
///
/// Config (environment variables shown in double-underscore form):
///   AI__GeminiApiKey  — Google AI Studio API key (required; endpoint 503s without it)
///   AI__Model         — model id, default "gemini-2.0-flash"
///   AI__BaseUrl       — default "https://generativelanguage.googleapis.com/v1beta"
///
/// Back-compat: if AI__GeminiApiKey is unset it falls back to AI__OpenAiApiKey,
/// so an existing deployment's secret name keeps working.
/// </summary>
public class GeminiReadingService : IAiReadingService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _cfg;
    private readonly ILogger<GeminiReadingService> _log;

    public GeminiReadingService(HttpClient http, IConfiguration cfg, ILogger<GeminiReadingService> log)
    {
        _http = http;
        _cfg = cfg;
        _log = log;
    }

    // ── Prompt engineering: the astrologer persona + strict output contract ──────
    private const string SystemPrompt =
"""
You are Sayar Myo Thant Naing (ဆရာ မျိုးသန့်နိုင်), an expert Vedic Astrologer, Software
Engineer, and AI Developer based in Japan. Your tone is majestic, deeply empathetic,
logically sound, and highly professional. You write ENTIRELY in elegant, fluent, natural
Burmese (မြန်မာ) — never mix in English sentences (technical Sanskrit/Jyotish terms in
Burmese transliteration are fine, e.g. ဒသာ, အန္တရ်ဒသာ, အဋ္ဌကဝဂ်, ဆဒ္ဗလ).

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
        var apiKey = _cfg["AI:GeminiApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey)) apiKey = _cfg["AI:OpenAiApiKey"]; // back-compat secret name
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _log.LogWarning("AI reading requested but AI:GeminiApiKey is not configured.");
            return ApiResponse<AiReadingResponseDto>.Fail(
                "AI reading is not configured on the server yet.", 503);
        }

        var model = string.IsNullOrWhiteSpace(_cfg["AI:Model"]) ? "gemini-2.0-flash" : _cfg["AI:Model"]!;
        var baseUrl = (string.IsNullOrWhiteSpace(_cfg["AI:BaseUrl"])
            ? "https://generativelanguage.googleapis.com/v1beta"
            : _cfg["AI:BaseUrl"]!).TrimEnd('/');
        var url = $"{baseUrl}/models/{model}:generateContent";

        var userContent = BuildUserPrompt(req);

        var payload = new
        {
            systemInstruction = new { parts = new[] { new { text = SystemPrompt } } },
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text = userContent } } },
            },
            generationConfig = new
            {
                temperature = 0.8,
                maxOutputTokens = 2400,
                topP = 0.95,
            },
        };

        using var msg = new HttpRequestMessage(HttpMethod.Post, url);
        msg.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);
        msg.Content = new StringContent(
            JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        try
        {
            using var resp = await _http.SendAsync(msg, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning("Gemini returned {Status}: {Body}", (int)resp.StatusCode, Truncate(body, 600));
                var friendly = (int)resp.StatusCode switch
                {
                    400 => "AI provider rejected the request (check the model name / API key).",
                    401 or 403 => "AI provider rejected the API key.",
                    429 => "AI provider is rate-limiting requests. Please try again shortly.",
                    _ => $"AI provider error ({(int)resp.StatusCode}).",
                };
                return ApiResponse<AiReadingResponseDto>.Fail(friendly, 502);
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            // A safety filter can block the prompt before any candidate is produced.
            if (root.TryGetProperty("promptFeedback", out var pf)
                && pf.TryGetProperty("blockReason", out var br))
            {
                _log.LogWarning("Gemini blocked the prompt: {Reason}", br.GetString());
                return ApiResponse<AiReadingResponseDto>.Fail(
                    "The AI declined to answer this request. Please adjust the input and try again.", 502);
            }

            var text = ExtractText(root);
            if (string.IsNullOrWhiteSpace(text))
                return ApiResponse<AiReadingResponseDto>.Fail("The AI returned an empty reading.", 502);

            return ApiResponse<AiReadingResponseDto>.Ok(new AiReadingResponseDto
            {
                Markdown = text.Trim(),
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

    /// <summary>Concatenate all text parts of the first candidate's content.</summary>
    private static string ExtractText(JsonElement root)
    {
        if (!root.TryGetProperty("candidates", out var cands) || cands.ValueKind != JsonValueKind.Array || cands.GetArrayLength() == 0)
            return string.Empty;
        var first = cands[0];
        if (!first.TryGetProperty("content", out var content) || !content.TryGetProperty("parts", out var parts))
            return string.Empty;

        var sb = new StringBuilder();
        foreach (var part in parts.EnumerateArray())
            if (part.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String)
                sb.Append(t.GetString());
        return sb.ToString();
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
