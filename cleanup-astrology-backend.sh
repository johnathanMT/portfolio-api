#!/usr/bin/env bash
# ══════════════════════════════════════════════════════════════════════════════
# cleanup-astrology-backend.sh
# Remove the Vedin/astrology feature from the old portfolio API. The astrology app
# now runs on its OWN Render service + database (vedin-backend), so none of this
# belongs here anymore. Verified by cross-reference: no portfolio controller,
# service, repository or model references any of these types.
#
# ⚠️  This script only deletes FILES. You MUST also apply the 3 in-file edits
#     described in the accompanying plan (Program.cs, Data/AppDbContext.cs,
#     PortfolioApi.csproj) — the two changes are ONE atomic commit. After both,
#     run `dotnet build` and fix any compiler-reported leftover (there shouldn't
#     be any).
#
# Run from the backend repo root:   bash cleanup-astrology-backend.sh
# ══════════════════════════════════════════════════════════════════════════════
set -e
cd "$(dirname "$0")"

# ── Controllers (astrology + Vedin research) ──────────────────────────────────
git rm -f "Controllers/AstrologyController.cs"
git rm -f "Controllers/CustomerController.cs"
git rm -f "Controllers/ResearchController.cs"

# ── Services (astrology compute, AI readings, email, PDF) ─────────────────────
git rm -f "Services/AstrologyService.cs"
git rm -f "Services/GeminiReadingService.cs"
git rm -f "Services/OpenAiReadingService.cs"
git rm -f "Services/SmtpEmailService.cs"
git rm -f "Services/MiniPdf.cs"

# ── Interfaces (astrology-only) ───────────────────────────────────────────────
git rm -f "Interfaces/IAstrologyService.cs"
git rm -f "Interfaces/IAiReadingService.cs"
git rm -f "Interfaces/IEmailService.cs"

# ── Models (astrology + research entities) ────────────────────────────────────
git rm -f "Models/AiReading.cs"
git rm -f "Models/ConsultationMessage.cs"
git rm -f "Models/Customer.cs"
git rm -f "Models/CustomerChart.cs"
git rm -f "Models/PdfRequest.cs"
git rm -f "Models/QuerentChart.cs"
git rm -f "Models/ReadingRequest.cs"
git rm -f "Models/RemedyRequest.cs"
git rm -f "Models/ResearchJournalEntry.cs"
git rm -f "Models/ResearchPrediction.cs"

# ── DTOs (astrology, customer, research) ──────────────────────────────────────
git rm -rf "DTOs/Astrology"
git rm -rf "DTOs/Research"
git rm -f  "DTOs/Auth/CustomerDtos.cs"   # keep the rest of DTOs/Auth (admin auth)

# ── Security (field-level encryption used only by astrology PII) ──────────────
git rm -f "Security/FieldCrypto.cs"

echo ""
echo "Deleted 29 astrology files. Now apply the 3 in-file edits (Program.cs,"
echo "Data/AppDbContext.cs, PortfolioApi.csproj), then:  dotnet build"
