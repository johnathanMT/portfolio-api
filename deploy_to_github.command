#!/bin/bash
# ═══════════════════════════════════════════════════════
#  deploy_to_github.command — macOS compatible
#  Double-click in Finder to run. Creates GitHub repo
#  + commits and pushes all PortfolioApi source files.
# ═══════════════════════════════════════════════════════

TOKEN="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"
GITHUB_USER="johnathanMT"
REPO_NAME="portfolio-api"
REPO_DESC="Production-ready C# .NET 8 Web API for portfolio and blog system"

cd "$(dirname "$0")"
PORTFOLIO_DIR="$(pwd)"

echo ""
echo "╔══════════════════════════════════════════════════╗"
echo "║   PortfolioApi → GitHub Deployment Script        ║"
echo "╚══════════════════════════════════════════════════╝"
echo ""
echo "📁 Working directory: $PORTFOLIO_DIR"
echo ""

# ── Step 1: Create GitHub repo (macOS-compatible) ────────
echo "🔧 Step 1/4 — Creating GitHub repository '$REPO_NAME'..."

TMPFILE=$(mktemp)

HTTP_CODE=$(curl -s \
  -X POST \
  -H "Authorization: token $TOKEN" \
  -H "Accept: application/vnd.github.v3+json" \
  -H "Content-Type: application/json" \
  https://api.github.com/user/repos \
  -o "$TMPFILE" \
  -w "%{http_code}" \
  -d "{
    \"name\": \"$REPO_NAME\",
    \"description\": \"$REPO_DESC\",
    \"private\": false,
    \"auto_init\": false,
    \"has_issues\": true
  }")

BODY=$(cat "$TMPFILE")
rm -f "$TMPFILE"

if [ "$HTTP_CODE" = "201" ]; then
    echo "✅ Repository created: https://github.com/$GITHUB_USER/$REPO_NAME"
elif [ "$HTTP_CODE" = "422" ]; then
    echo "⚠️  Repository already exists — continuing with push..."
elif [ "$HTTP_CODE" = "000" ]; then
    echo "❌ Network error — no response from GitHub API."
    echo "   Check your internet connection and try again."
    read -p "Press ENTER to exit..."
    exit 1
else
    echo "❌ Unexpected HTTP $HTTP_CODE from GitHub API"
    echo "Response: $BODY"
    read -p "Press ENTER to exit..."
    exit 1
fi

echo ""

# ── Step 2: Init git ─────────────────────────────────────
echo "🔧 Step 2/4 — Initialising local git repository..."

rm -rf .git
git init -b main
git config user.email "myothantnaing1178@gmail.com"
git config user.name "Myo Thant Naing"

echo "✅ Git initialised (branch: main)"
echo ""

# ── Step 3: Stage and commit ─────────────────────────────
echo "🔧 Step 3/4 — Staging and committing all source files..."

git add -A
git status --short
git commit -m "feat: production-ready C# .NET 8 Portfolio API

Architecture: Repository + Service Pattern (SOLID principles)
Security: JWT, BCrypt (cost 12), Rate Limiting, CORS, XSS sanitise
Database: MySQL on Aiven via Pomelo EF Core
Images: Cloudinary upload (ephemeral FS safe for Render)
Docs: Full Swagger/OpenAPI + XML comments
Deploy: Docker multi-stage build for Render"

echo "✅ All files committed"
echo ""

# ── Step 4: Push to GitHub ───────────────────────────────
echo "🔧 Step 4/4 — Pushing to GitHub..."

git remote add origin "https://${TOKEN}@github.com/${GITHUB_USER}/${REPO_NAME}.git"
git push -u origin main --force

echo ""
echo "╔══════════════════════════════════════════════════╗"
echo "║   ✅ DEPLOYMENT COMPLETE!                        ║"
echo "╚══════════════════════════════════════════════════╝"
echo ""
echo "🔗 Repo:  https://github.com/$GITHUB_USER/$REPO_NAME"
echo ""
echo "── NEXT: Deploy on Render ─────────────────────────"
echo "  1. https://render.com → New → Web Service"
echo "  2. Connect: github.com/$GITHUB_USER/$REPO_NAME"
echo "  3. Runtime: Docker (auto-detected)"
echo "  4. Add the environment variables listed below"
echo ""
echo "── Environment Variables for Render ──────────────"
echo "  ASPNETCORE_ENVIRONMENT              = Production"
echo "  ConnectionStrings__DefaultConnection = <Aiven URL>"
echo "  Jwt__Key                            = <32+ char secret>"
echo "  Jwt__Issuer                         = PortfolioApi"
echo "  Jwt__Audience                       = PortfolioApiUsers"
echo "  Jwt__ExpirationHours                = 24"
echo "  AdminSecret                         = <your secret>"
echo "  Cloudinary__CloudName               = <cloudinary>"
echo "  Cloudinary__ApiKey                  = <cloudinary>"
echo "  Cloudinary__ApiSecret               = <cloudinary>"
echo "  Cors__AllowedOrigins__0             = https://johnathanmt.github.io"
echo ""

read -p "Press ENTER to close..."
