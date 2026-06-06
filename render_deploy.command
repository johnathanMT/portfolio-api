#!/bin/bash
# ═══════════════════════════════════════════════════════════════
#  render_deploy.command — Full automated Render deployment
#  Double-click in Finder to run.
#  Creates the Web Service + sets all environment variables.
# ═══════════════════════════════════════════════════════════════

set -e

REPO_URL="https://github.com/johnathanMT/portfolio-api"
SERVICE_NAME="portfolio-api"
REGION="singapore"

echo ""
echo "╔══════════════════════════════════════════════════════════╗"
echo "║   PortfolioApi → Render Deployment Script               ║"
echo "╚══════════════════════════════════════════════════════════╝"
echo ""
echo "This script will:"
echo "  1. Connect to your Render account via API"
echo "  2. Create a Docker Web Service for portfolio-api"
echo "  3. Set all environment variables"
echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "📋 STEP 1 — Get your Render API Key:"
echo "   1. Open: https://dashboard.render.com/u/settings#api-keys"
echo "   2. Click 'Create API Key'"
echo "   3. Copy the key (starts with rnd_...)"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""
read -s -p "Paste your Render API key (hidden): " RENDER_KEY
echo ""
echo ""

if [ -z "$RENDER_KEY" ]; then
    echo "❌ No API key entered. Exiting."
    read -p "Press ENTER to close..."; exit 1
fi

echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "✅ STEP 2 — Database (Aiven MySQL) — pre-filled!"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "   Using your Aiven connection string (already configured)."
echo ""

echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "📋 STEP 3 — JWT Secret Key"
echo "   Type any long random string (40+ characters)."
echo "   Example: MyPortfolioJwtSecretKey2024!SuperSecure@Render"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""
read -s -p "Enter a JWT secret key (min 32 chars): " JWT_KEY
echo ""
echo ""

echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "📋 STEP 4 — Admin Secret"
echo "   Used to register as Admin via POST /api/auth/register"
echo "   Choose any strong password you'll remember."
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""
read -s -p "Enter your Admin secret: " ADMIN_SECRET
echo ""
echo ""

echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "📋 STEP 5 — Cloudinary credentials"
echo "   Get these from: https://cloudinary.com → Dashboard"
echo "   (Free account is fine — 25 GB storage)"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""
read -p "Cloudinary Cloud Name: " CLOUD_NAME
read -s -p "Cloudinary API Key: " CLOUD_API_KEY
echo ""
read -s -p "Cloudinary API Secret: " CLOUD_API_SECRET
echo ""
echo ""

# ── Validate required inputs ──────────────────────────────────
echo "🔍 Validating inputs..."
ERRORS=0
[ -z "$DB_CONN" ]         && echo "  ❌ Database connection string is empty" && ERRORS=$((ERRORS+1))
[ -z "$JWT_KEY" ]         && echo "  ❌ JWT key is empty" && ERRORS=$((ERRORS+1))
[ ${#JWT_KEY} -lt 32 ]    && echo "  ❌ JWT key must be at least 32 characters" && ERRORS=$((ERRORS+1))
[ -z "$ADMIN_SECRET" ]    && echo "  ❌ Admin secret is empty" && ERRORS=$((ERRORS+1))
[ -z "$CLOUD_NAME" ]      && echo "  ❌ Cloudinary cloud name is empty" && ERRORS=$((ERRORS+1))
[ -z "$CLOUD_API_KEY" ]   && echo "  ❌ Cloudinary API key is empty" && ERRORS=$((ERRORS+1))
[ -z "$CLOUD_API_SECRET" ] && echo "  ❌ Cloudinary API secret is empty" && ERRORS=$((ERRORS+1))

if [ "$ERRORS" -gt 0 ]; then
    echo ""
    echo "Please fix the above errors and run the script again."
    read -p "Press ENTER to close..."; exit 1
fi
echo "  ✅ All inputs provided"
echo ""

# ── Step 1: Get Render owner ID ───────────────────────────────
echo "🔧 Connecting to Render API..."
TMPFILE=$(mktemp)

HTTP_CODE=$(curl -s \
    -H "Authorization: Bearer $RENDER_KEY" \
    -H "Accept: application/json" \
    "https://api.render.com/v1/owners?limit=1" \
    -o "$TMPFILE" \
    -w "%{http_code}")

if [ "$HTTP_CODE" != "200" ]; then
    echo "❌ Render API error (HTTP $HTTP_CODE). Check your API key."
    cat "$TMPFILE"
    rm -f "$TMPFILE"
    read -p "Press ENTER to close..."; exit 1
fi

OWNER_ID=$(python3 -c "
import json, sys
data = json.load(open('$TMPFILE'))
if isinstance(data, list) and len(data) > 0:
    print(data[0]['owner']['id'])
elif isinstance(data, dict) and 'id' in data:
    print(data['id'])
else:
    print('')
" 2>/dev/null)
rm -f "$TMPFILE"

if [ -z "$OWNER_ID" ]; then
    echo "❌ Could not retrieve Render owner ID. Check your API key."
    read -p "Press ENTER to close..."; exit 1
fi

echo "✅ Connected to Render. Owner ID: $OWNER_ID"
echo ""

# ── Step 2: Check if service already exists ───────────────────
echo "🔧 Checking for existing service..."
TMPFILE=$(mktemp)

curl -s \
    -H "Authorization: Bearer $RENDER_KEY" \
    "https://api.render.com/v1/services?name=$SERVICE_NAME&limit=5" \
    -o "$TMPFILE"

EXISTING_ID=$(python3 -c "
import json
data = json.load(open('$TMPFILE'))
services = data if isinstance(data, list) else []
for s in services:
    svc = s.get('service', s)
    if svc.get('name') == '$SERVICE_NAME':
        print(svc.get('id',''))
        break
" 2>/dev/null)
rm -f "$TMPFILE"

if [ -n "$EXISTING_ID" ]; then
    echo "⚠️  Service '$SERVICE_NAME' already exists (ID: $EXISTING_ID). Updating env vars..."
    SERVICE_ID="$EXISTING_ID"
else
    # ── Step 3: Create the Web Service ──────────────────────────
    echo "🔧 Creating Render Web Service..."
    TMPFILE=$(mktemp)

    PAYLOAD=$(python3 -c "
import json
payload = {
    'type': 'web_service',
    'name': '$SERVICE_NAME',
    'ownerId': '$OWNER_ID',
    'repo': '$REPO_URL',
    'branch': 'main',
    'autoDeploy': 'yes',
    'serviceDetails': {
        'env': 'docker',
        'dockerfilePath': './Dockerfile',
        'plan': 'free',
        'region': '$REGION',
        'healthCheckPath': '/health'
    }
}
print(json.dumps(payload))
")

    HTTP_CODE=$(curl -s \
        -X POST \
        -H "Authorization: Bearer $RENDER_KEY" \
        -H "Content-Type: application/json" \
        -H "Accept: application/json" \
        "https://api.render.com/v1/services" \
        -d "$PAYLOAD" \
        -o "$TMPFILE" \
        -w "%{http_code}")

    if [ "$HTTP_CODE" != "201" ] && [ "$HTTP_CODE" != "200" ]; then
        echo "❌ Failed to create service (HTTP $HTTP_CODE):"
        cat "$TMPFILE"
        rm -f "$TMPFILE"
        read -p "Press ENTER to close..."; exit 1
    fi

    SERVICE_ID=$(python3 -c "
import json
data = json.load(open('$TMPFILE'))
svc = data.get('service', data)
print(svc.get('id',''))
" 2>/dev/null)
    rm -f "$TMPFILE"

    echo "✅ Web Service created! ID: $SERVICE_ID"
fi

echo ""

# ── Step 4: Set environment variables ────────────────────────
echo "🔧 Setting all environment variables..."

TMPFILE=$(mktemp)

ENV_PAYLOAD=$(python3 -c "
import json
envs = [
    {'key': 'ASPNETCORE_ENVIRONMENT',             'value': 'Production'},
    {'key': 'ConnectionStrings__DefaultConnection','value': '''$DB_CONN'''},
    {'key': 'Jwt__Key',                           'value': '''$JWT_KEY'''},
    {'key': 'Jwt__Issuer',                        'value': 'PortfolioApi'},
    {'key': 'Jwt__Audience',                      'value': 'PortfolioApiUsers'},
    {'key': 'Jwt__ExpirationHours',               'value': '24'},
    {'key': 'AdminSecret',                        'value': '''$ADMIN_SECRET'''},
    {'key': 'Cloudinary__CloudName',              'value': '$CLOUD_NAME'},
    {'key': 'Cloudinary__ApiKey',                 'value': '''$CLOUD_API_KEY'''},
    {'key': 'Cloudinary__ApiSecret',              'value': '''$CLOUD_API_SECRET'''},
    {'key': 'Cors__AllowedOrigins__0',            'value': 'https://johnathanmt.github.io'},
    {'key': 'RateLimit__GeneralPermitLimit',      'value': '100'},
    {'key': 'RateLimit__GeneralWindowSeconds',    'value': '60'},
    {'key': 'RateLimit__AuthPermitLimit',         'value': '10'},
    {'key': 'RateLimit__AuthWindowSeconds',       'value': '900'},
]
print(json.dumps(envs))
")

HTTP_CODE=$(curl -s \
    -X PUT \
    -H "Authorization: Bearer $RENDER_KEY" \
    -H "Content-Type: application/json" \
    "https://api.render.com/v1/services/$SERVICE_ID/env-vars" \
    -d "$ENV_PAYLOAD" \
    -o "$TMPFILE" \
    -w "%{http_code}")

rm -f "$TMPFILE"

if [ "$HTTP_CODE" = "200" ] || [ "$HTTP_CODE" = "204" ]; then
    echo "✅ All 15 environment variables set successfully!"
else
    echo "⚠️  Env vars HTTP $HTTP_CODE — check manually in Render dashboard."
fi

echo ""

# ── Step 5: Trigger deploy ────────────────────────────────────
echo "🔧 Triggering initial deployment..."
TMPFILE=$(mktemp)

HTTP_CODE=$(curl -s \
    -X POST \
    -H "Authorization: Bearer $RENDER_KEY" \
    -H "Content-Type: application/json" \
    "https://api.render.com/v1/services/$SERVICE_ID/deploys" \
    -d '{"clearCache":"do_not_clear"}' \
    -o "$TMPFILE" \
    -w "%{http_code}")

DEPLOY_ID=$(python3 -c "
import json
try:
    d = json.load(open('$TMPFILE'))
    print(d.get('id',''))
except: print('')
" 2>/dev/null)
rm -f "$TMPFILE"

if [ "$HTTP_CODE" = "201" ] || [ "$HTTP_CODE" = "200" ]; then
    echo "✅ Deployment triggered! Deploy ID: $DEPLOY_ID"
else
    echo "⚠️  Trigger HTTP $HTTP_CODE — deploy may start automatically."
fi

echo ""
echo "╔══════════════════════════════════════════════════════════╗"
echo "║   🚀 RENDER DEPLOYMENT LAUNCHED!                        ║"
echo "╚══════════════════════════════════════════════════════════╝"
echo ""
echo "📊 Monitor your deployment:"
echo "   https://dashboard.render.com/web/$SERVICE_ID"
echo ""
echo "🔗 Your API will be live at:"
echo "   https://$SERVICE_NAME.onrender.com"
echo ""
echo "   First build takes ~3-5 minutes (Docker build + push)."
echo "   Free tier sleeps after 15 min inactivity (cold start ~30s)."
echo ""
echo "✅ Verify deployment:"
echo "   GET https://$SERVICE_NAME.onrender.com/health"
echo "   GET https://$SERVICE_NAME.onrender.com/         ← Swagger UI"
echo ""
echo "👤 Register your Admin account:"
echo "   POST https://$SERVICE_NAME.onrender.com/api/auth/register"
echo "   Body: {\"username\":\"myo\", \"email\":\"myothantnaing1178@gmail.com\","
echo "          \"password\":\"YourPassword\", \"adminSecret\":\"<your admin secret>\"}"
echo ""

read -p "Press ENTER to close..."
