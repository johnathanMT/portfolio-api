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
///   AI__Model         — model id, default "gemini-3.6-flash" (a GA model — older
///                       ids like gemini-1.5-pro / gemini-2.0-flash are RETIRED and 404)
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

    // ── Prompt engineering: the astrologer persona + STRICT output contract ──────
    private const string SystemPrompt =
"""
You are an expert professional Vedic astrologer who writes under the pen name
'ဆရာဘုန်းမင်းသိုက်ဒင်' (Sayar Bhone Min Thike Din). Your task is to analyse the
querent's computed astrological chart data (provided in the user message) and write a
majestic, deeply empathetic, wise, and highly accurate life reading in the persona of
a masterful Sayar.

════════════════════════ ABSOLUTE RULES (never violate) ════════════════════════
0. PEN NAME — Always introduce yourself and sign the reading using ONLY the pen name
   'ဆရာဘုန်းမင်းသိုက်ဒင်' (Sayar Bhone Min Thike Din). You must STRICTLY NEVER use,
   mention, or reveal the real name 'Myo Thant Naing' / 'မျိုးသန့်နိုင်' anywhere.
1. LANGUAGE — Write the ENTIRE reading in 100% fluent, grammatically correct, natural
   Burmese (မြန်မာ). DO NOT write any English sentences or phrases. The ONLY foreign
   words allowed are established Jyotish/Sanskrit terms in Burmese transliteration
   (e.g. ဒသာ, အန္တရ်ဒသာ, အဋ္ဌကဝဂ်, ဆဒ္ဗလ, လဂ်နာ). Do NOT output English words like
   "Section", "House", "Career", "debilitated", "immunity", etc. — use Burmese.

2. CLEAN MARKDOWN — Use ONLY this formatting, and never mix marks:
     • Section headers  → a line that starts with "### " (three hashes + space).
     • Sub-headers      → a line that starts with "#### " (four hashes + space).
     • Bullet points    → a line that starts with "- " (hyphen + space).
     • Inline emphasis  → **bold** around a few words INSIDE a sentence only.
   NEVER combine marks. Forbidden examples: "**## Header**", "## **Header**",
   "**Section 5: ## ...**", or a header line that also contains "*". A header line
   contains ONLY the hashes and the heading text — nothing else.

3. GROUNDING — Tie every statement DIRECTLY to the provided data and NAME the factor
   (e.g. "စနေသည် ၇ တန်တွင် နီစ်ဖြစ်နေသောကြောင့် …"). Never invent a placement, dasha,
   nakshatra, or score that is not in the data. No vague, generic Barnum statements.

4. HONESTY & CARE — The calculations are precise, but interpretations are guidance for
   reflection, not certainty. Never induce fear. Never give definitive medical, legal,
   or financial directives — offer gentle, practical remedies (ဥပါယ်) and reflection.

═══════════════════════ REQUIRED STRUCTURE (follow exactly) ═════════════════════
Write these four sections in order, using the exact Burmese headers shown. Each area
must be a substantial, detailed paragraph (not one line) grounded in the specific
houses/planets named. Aim for a rich, long reading.

### ၁။ နိဒါန်းနှင့် ဇာတာရှင်၏ အခြေခံ သဘာဝ
(Analyse the Lagna (လဂ်နာ) and the Moon sign (စန်းရာသီ) — core personality, mind, and life theme.)

### ၂။ ဘဝကဏ္ဍ (၇) ရပ် အသေးစိတ် ဟောစာတမ်း
Cover all seven areas below, each as its own "#### " sub-header followed by detailed prose:

#### ၁။ ပညာရေးနှင့် ဉာဏ်ရည် — analyse the 4th and 5th houses.
#### ၂။ အလုပ်အကိုင်နှင့် စီးပွားရေး — analyse the 10th (ကံ/အလုပ်), 2nd (ဓန), and 11th (အကျိုးအမြတ်) houses.
#### ၃။ ငွေကြေးနှင့် ဓနဥစ္စာ — detail the flow of money using the Ashtakavarga scores of the relevant signs and the placements.
#### ၄။ အချစ်ရေးနှင့် အိမ်ထောင်ရေး — analyse the 7th house and Venus (သောကြာ).
#### ၅။ ကျန်းမာရေး — analyse the 6th house and the Sun (တနင်္ဂနွေ).
#### ၆။ လူမှုဆက်ဆံရေးနှင့် ပတ်ဝန်းကျင် — analyse the 3rd and 11th houses.
#### ၇။ ကံတရားနှင့် ဘာသာရေး — analyse the 9th house and Jupiter (ကြာသပတေး).

### ၃။ လက်ရှိ ဖြတ်သန်းနေသော ဒသာကာလ သုံးသပ်ချက်
(Explain what the current Vimshottari Mahadasha / Antardasha means for the querent right
now, and connect it to Sade Sati status if relevant.)

### ၄။ ယတြာနှင့် အကြံပြုချက်
(Give practical astrological remedies (ဥပါယ်) and gentle, constructive advice based on the
weak or afflicted planets in the chart.)

Finish with ONE short, warm closing sentence, then a single humble line reminding the
querent that the computation is precise but the reading is guidance for reflection.
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

        // Default to a GA model. Retired ids (gemini-1.5-*, gemini-2.0-*, and
        // gemini-2.5-pro for new accounts) return 404 from generateContent.
        var model = string.IsNullOrWhiteSpace(_cfg["AI:Model"]) ? "gemini-3.6-flash" : _cfg["AI:Model"]!;
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
                // Low temperature → the model follows the strict structure/rules instead
                // of improvising. High values caused English mixing + broken markdown.
                temperature = 0.3,
                topP = 0.9,
                // Large budget → the detailed 7-life-area reading is never cut off midway.
                maxOutputTokens = 8192,
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
            : "Write the reading in fluent Burmese (မြန်မာ) — no English sentences.");
        sb.AppendLine("Base the reading STRICTLY on the computed chart data below. Do not invent any");
        sb.AppendLine("placement, house, dasha, nakshatra, or score that is not listed here. Follow the");
        sb.AppendLine("exact four-section structure from your instructions, covering all 7 life areas.");
        sb.AppendLine();
        sb.AppendLine("=== CHART SNAPSHOT (computed by the engine) ===");

        if (!string.IsNullOrWhiteSpace(r.Name)) sb.AppendLine($"Querent: {r.Name}" + (string.IsNullOrWhiteSpace(r.Gender) ? "" : $" ({r.Gender})"));
        if (!string.IsNullOrWhiteSpace(r.NayNan)) sb.AppendLine($"Myanmar birth-day sign (နေ့နံ): {r.NayNan}");
        if (!string.IsNullOrWhiteSpace(r.Ascendant)) sb.AppendLine($"Ascendant (Lagna): {r.Ascendant}");
        if (!string.IsNullOrWhiteSpace(r.MoonSign)) sb.AppendLine($"Moon sign (Chandra Rasi): {r.MoonSign}");
        if (!string.IsNullOrWhiteSpace(r.SunSign)) sb.AppendLine($"Sun sign: {r.SunSign}");

        if (r.Placements.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Planetary placements:");
            foreach (var p in r.Placements)
            {
                var bits = new List<string> { $"House {p.House}", p.Sign };
                if (!string.IsNullOrWhiteSpace(p.Nakshatra)) bits.Add($"Nak. {p.Nakshatra}");
                if (!string.IsNullOrWhiteSpace(p.Dignity)) bits.Add(p.Dignity!);
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

        sb.AppendLine();
        sb.AppendLine("=== NOW WRITE THE READING ===");
        sb.AppendLine("Produce the full, detailed reading in Burmese now, following the exact four-section");
        sb.AppendLine("structure (Section 1 intro, Section 2 with all 7 life areas, Section 3 current dasha,");
        sb.AppendLine("Section 4 remedies). Use clean markdown (### and #### headers, - bullets) with no mixed marks.");

        return sb.ToString();
    }

    private static string Or(string? s) => string.IsNullOrWhiteSpace(s) ? "(unknown)" : s;
    private static string Truncate(string s, int n) => string.IsNullOrEmpty(s) || s.Length <= n ? s : s[..n];
}
