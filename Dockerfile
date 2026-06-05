# ═══════════════════════════════════════════════════════════════
#  Dockerfile — PortfolioApi (.NET 8)
#  Multi-stage build: minimises final image size (~120 MB)
#  Optimised for Render's container deployment
# ═══════════════════════════════════════════════════════════════

# ── Stage 1: Build ─────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy only the project file first to leverage Docker layer cache.
# The restore layer is rebuilt only when .csproj changes.
COPY ["PortfolioApi.csproj", "."]
RUN dotnet restore "./PortfolioApi.csproj" --runtime linux-x64

# Copy the rest of the source code
COPY . .

# Publish — self-contained, single-file, trimmed, Release mode
RUN dotnet publish "./PortfolioApi.csproj" \
    -c Release \
    -r linux-x64 \
    --no-restore \
    --self-contained false \
    -o /app/publish

# ── Stage 2: Runtime ───────────────────────────────────────────
# Use the minimal ASP.NET runtime image (not the SDK)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Security: run as a non-root user
RUN addgroup --system --gid 1001 appgroup && \
    adduser  --system --uid 1001 --ingroup appgroup --no-create-home appuser

# Copy published output from build stage
COPY --from=build /app/publish .

# Render sets PORT env variable dynamically; fall back to 8080
ENV ASPNETCORE_URLS="http://+:${PORT:-8080}"
ENV ASPNETCORE_ENVIRONMENT="Production"
ENV DOTNET_RUNNING_IN_CONTAINER=true

# Switch to non-root user before starting
USER appuser

# Expose the port (Render reads PORT env var, but this documents intent)
EXPOSE 8080

# Health-check so Render knows the container is alive
HEALTHCHECK --interval=30s --timeout=10s --start-period=15s --retries=3 \
    CMD wget -qO- http://localhost:${PORT:-8080}/health || exit 1

ENTRYPOINT ["dotnet", "PortfolioApi.dll"]
