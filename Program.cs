// ═══════════════════════════════════════════════════════════════════════════
//  Program.cs — PortfolioApi (.NET 8)
//  Architecture : Repository + Service Pattern
//  Security     : JWT, BCrypt, CORS, Rate Limiting, Input Sanitisation
//  Database     : MySQL on Aiven via Pomelo EF Core
//  Images       : Cloudinary (ephemeral FS safe for Render deployment)
// ═══════════════════════════════════════════════════════════════════════════

using System.Text;
using System.Threading.RateLimiting;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PortfolioApi.Data;
using PortfolioApi.Interfaces;
using PortfolioApi.Middleware;
using PortfolioApi.Models;
using PortfolioApi.Repositories;
using PortfolioApi.Services;
using PortfolioApi.Validators;

var builder = WebApplication.CreateBuilder(args);

// ─────────────────────────────────────────────────────────────
// 0. REVERSE PROXY (Render/Vercel terminate TLS) + HSTS
//    Render's edge proxy terminates HTTPS, so the app must trust the
//    X-Forwarded-Proto/-For headers to know the real scheme + client IP
//    (needed for correct HTTPS redirect, secure cookies, and per-IP limits).
// ─────────────────────────────────────────────────────────────
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // The proxy IP is not fixed on Render; trust the platform edge.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Strict-Transport-Security: force HTTPS for a year, incl. subdomains, preloadable.
builder.Services.AddHsts(options =>
{
    options.Preload = true;
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(365);
});

// ─────────────────────────────────────────────────────────────
// 1. DATABASE  — Pomelo MySQL with Aiven connection string
// ─────────────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(
        connectionString,
        // Pinned instead of AutoDetect: no design-time/cold-start DB call needed.
        new MySqlServerVersion(new Version(8, 0, 35)),
        mySqlOptions =>
        {
            mySqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);
            mySqlOptions.CommandTimeout(30);
        }));

// ─────────────────────────────────────────────────────────────
// 2. DEPENDENCY INJECTION — Repositories & Services
// ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IArticleRepository, ArticleRepository>();
builder.Services.AddScoped<IInteractionRepository, InteractionRepository>();
builder.Services.AddScoped<IPoemRepository, PoemRepository>();
builder.Services.AddScoped<IMemoryRepository, MemoryRepository>();
builder.Services.AddScoped<IFarewellRepository, FarewellRepository>();
builder.Services.AddScoped<IVisitorRepository, VisitorRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IArticleService, ArticleService>();
builder.Services.AddScoped<IInteractionService, InteractionService>();
builder.Services.AddScoped<IPoemService, PoemService>();
builder.Services.AddScoped<IMemoryService, MemoryService>();
builder.Services.AddScoped<IFarewellService, FarewellService>();
builder.Services.AddScoped<IVisitorService, VisitorService>();
builder.Services.AddScoped<IAstrologyService, AstrologyService>();
builder.Services.AddSingleton<IImageService, CloudinaryImageService>();
builder.Services.AddScoped<IEmailService, SmtpEmailService>();
builder.Services.AddMemoryCache();   // resend-confirmation anti-spam throttling

// AI reading: typed HttpClient → Google Gemini (generativelanguage.googleapis.com).
// Config via AI__GeminiApiKey / AI__Model (default gemini-2.0-flash) / AI__BaseUrl.
// 60s timeout (LLMs are slow). To switch back to an OpenAI-compatible provider,
// register OpenAiReadingService instead (it remains in the codebase).
builder.Services.AddHttpClient<IAiReadingService, GeminiReadingService>(c =>
{
    c.Timeout = TimeSpan.FromSeconds(60);
});

// ─────────────────────────────────────────────────────────────
// 3. FLUENT VALIDATION
// ─────────────────────────────────────────────────────────────
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<RegisterDtoValidator>();

// ─────────────────────────────────────────────────────────────
// 4. JWT AUTHENTICATION
// ─────────────────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("JWT Key not configured.");

if (jwtKey.Length < 32)
    throw new InvalidOperationException("JWT Key must be at least 32 characters.");

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero, // No tolerance for expired tokens
        };

        // Return JSON on 401/403 instead of an empty body
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async ctx =>
            {
                ctx.HandleResponse();
                ctx.Response.StatusCode = 401;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync(
                    """{"success":false,"message":"Authentication required. Please provide a valid JWT token.","statusCode":401}""");
            },
            OnForbidden = async ctx =>
            {
                ctx.Response.StatusCode = 403;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync(
                    """{"success":false,"message":"You do not have permission to perform this action. Admin role required.","statusCode":403}""");
            },
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

// ─────────────────────────────────────────────────────────────
// 5. CORS — Strict whitelist of allowed origins
// ─────────────────────────────────────────────────────────────
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("PortfolioCors", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
              .AllowCredentials();
    });

    // Locked-down policy for Swagger UI in production (adjust as needed)
    options.AddPolicy("SwaggerCors", policy =>
        policy.WithOrigins("https://localhost:5001", "http://localhost:5000")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// ─────────────────────────────────────────────────────────────
// 6. RATE LIMITING (.NET 8 built-in — no extra package needed)
// ─────────────────────────────────────────────────────────────
var rlSection = builder.Configuration.GetSection("RateLimit");

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // GLOBAL safety net — every endpoint (incl. Admin/Share/Health and anything
    // added later) is capped per client IP, even without an [EnableRateLimiting]
    // attribute. Named policies below stack ON TOP for stricter routes.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromSeconds(60),
                PermitLimit = 200,
                QueueLimit = 0,
            }));

    // General API policy: 100 req / 60 s
    options.AddFixedWindowLimiter("general", opt =>
    {
        opt.Window = TimeSpan.FromSeconds(rlSection.GetValue("GeneralWindowSeconds", 60));
        opt.PermitLimit = rlSection.GetValue("GeneralPermitLimit", 100);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });

    // Auth endpoints policy: 10 req / 15 min (brute-force protection)
    options.AddFixedWindowLimiter("auth", opt =>
    {
        opt.Window = TimeSpan.FromSeconds(rlSection.GetValue("AuthWindowSeconds", 900));
        opt.PermitLimit = rlSection.GetValue("AuthPermitLimit", 10);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });

    // Anonymous interactions policy: 30 req / 60 s (like/react spam protection)
    options.AddFixedWindowLimiter("interactions", opt =>
    {
        opt.Window = TimeSpan.FromSeconds(rlSection.GetValue("InteractionsWindowSeconds", 60));
        opt.PermitLimit = rlSection.GetValue("InteractionsPermitLimit", 30);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });

    // Astrology chart: heavy compute per request (full natal chart + the 80-year
    // transit timeline ≈ 250 ephemeris calls) → tighter per-IP cap to prevent
    // CPU-exhaustion abuse of the public, unauthenticated endpoint.
    options.AddFixedWindowLimiter("astrology", opt =>
    {
        opt.Window = TimeSpan.FromSeconds(rlSection.GetValue("AstrologyWindowSeconds", 60));
        opt.PermitLimit = rlSection.GetValue("AstrologyPermitLimit", 20);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });

    // AI reading: each call fans out to a paid LLM provider, so cap it tightly
    // per-IP to prevent cost-exhaustion abuse of the public endpoint.
    options.AddFixedWindowLimiter("ai", opt =>
    {
        opt.Window = TimeSpan.FromSeconds(rlSection.GetValue("AiWindowSeconds", 60));
        opt.PermitLimit = rlSection.GetValue("AiPermitLimit", 5);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });

    // Sanctuary memory writes: very tight token bucket (a human leaves ONE
    // memory, not hundreds) → stops AI/bot spam on POST /api/sanctuary/memories.
    options.AddTokenBucketLimiter("memory-write", opt =>
    {
        opt.TokenLimit = rlSection.GetValue("MemoryWriteBurst", 5);
        opt.TokensPerPeriod = 1;
        opt.ReplenishmentPeriod = TimeSpan.FromSeconds(rlSection.GetValue("MemoryWriteSeconds", 20));
        opt.AutoReplenishment = true;
        opt.QueueLimit = 0;
    });

    // Graceful rejection response
    options.OnRejected = async (ctx, _) =>
    {
        ctx.HttpContext.Response.ContentType = "application/json";
        await ctx.HttpContext.Response.WriteAsync(
            """{"success":false,"message":"Too many requests. Please slow down and try again later.","statusCode":429}""");
    };
});

// ─────────────────────────────────────────────────────────────
// 7. SWAGGER / OPENAPI with JWT support
// ─────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "MTN Portfolio API",
        Version = "v1",
        Description = "Production-ready REST API for Myo Thant Naing's personal portfolio and blog system.",
        Contact = new OpenApiContact
        {
            Name = "Myo Thant Naing",
            Email = "myothantnaing1178@gmail.com",
            Url = new Uri("https://johnathanmt.github.io/Myweb/"),
        },
    });

    // Add JWT Bearer button to Swagger UI
    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Enter: **Bearer {your_jwt_token}**",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference
        {
            Id = JwtBearerDefaults.AuthenticationScheme,
            Type = ReferenceType.SecurityScheme,
        },
    };
    options.AddSecurityDefinition(securityScheme.Reference.Id, securityScheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { securityScheme, Array.Empty<string>() }
    });
});

// ─────────────────────────────────────────────────────────────
// 8. MULTIPART FORM DATA limits (for image uploads)
// ─────────────────────────────────────────────────────────────
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(opt =>
{
    opt.MultipartBodyLengthLimit = 120 * 1024 * 1024; // up to 120 MB (covers a ~100 MB video)
});

// Allow large request bodies through Kestrel for video uploads
builder.WebHost.ConfigureKestrel(opt =>
{
    opt.Limits.MaxRequestBodySize = 120 * 1024 * 1024;
});

// ─────────────────────────────────────────────────────────────
// BUILD THE APPLICATION
// ─────────────────────────────────────────────────────────────
var app = builder.Build();

// ─────────────────────────────────────────────────────────────
// 9. MIDDLEWARE PIPELINE (order matters)
// ─────────────────────────────────────────────────────────────
var isDev = app.Environment.IsDevelopment();

// A. Reverse-proxy headers FIRST — real scheme + client IP for everything below.
app.UseForwardedHeaders();

// B. Global exception handler — early so it catches everything that follows.
app.UseMiddleware<ExceptionMiddleware>();

// C. Security headers (applied to EVERY response)
app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h.Append("X-Content-Type-Options", "nosniff");
    h.Append("X-Frame-Options", "DENY");
    h.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    h.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
    h.Append("Cross-Origin-Opener-Policy", "same-origin");
    h.Append("Cross-Origin-Resource-Policy", "same-site");
    // (X-XSS-Protection intentionally omitted — deprecated/harmful; CSP replaces it.)
    // This is a JSON API (Swagger is dev-only), so lock content sources down in prod.
    if (!isDev)
        h.Append("Content-Security-Policy", "default-src 'none'; frame-ancestors 'none'; base-uri 'none'");
    await next();
});

// D. HSTS (prod only — never over plain-HTTP localhost).
if (!isDev) app.UseHsts();

// E. HTTPS redirection
app.UseHttpsRedirection();

// F. Swagger — DEVELOPMENT ONLY (never publish the full API surface in prod).
if (isDev)
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "MTN Portfolio API v1");
        c.RoutePrefix = string.Empty; // Swagger at root /
        c.DocumentTitle = "MTN Portfolio API";
        c.DefaultModelsExpandDepth(-1);
    });
}

// G. Rate limiter
app.UseRateLimiter();

// H. CORS — must be before Auth
app.UseCors("PortfolioCors");

// I. Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// J. Controllers
app.MapControllers();

// ─────────────────────────────────────────────────────────────
// 10. AUTO-MIGRATE ON STARTUP (safe for Render cold starts)
// ─────────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        if (pending.Count > 0)
        {
            logger.LogInformation("Applying {Count} pending migration(s): {Names}",
                pending.Count, string.Join(", ", pending));
            await db.Database.MigrateAsync();
            logger.LogInformation("Database migrations applied successfully.");
        }
        else
        {
            logger.LogWarning("No pending migrations found. If tables are missing, " +
                "the Migrations folder may not be deployed to Render.");
        }
    }
    catch (Exception ex)
    {
        // Non-fatal: log and keep booting so the API still starts and GET /health
        // can report `database: disconnected` (far easier to diagnose than a dead
        // service that returns nothing). A transient DB blip no longer takes the
        // entire site down — DB-backed endpoints will surface 5xx until the
        // database is reachable again, but the process stays alive.
        logger.LogError(ex, "Failed to apply migrations. Check connection string / Aiven IP access. " +
            "Starting anyway so /health is reachable; DB-backed endpoints will fail until the DB recovers.");
    }

    // Astrology tables live outside EF migrations — ensure they exist idempotently
    // (encrypted-at-rest PII: birth details & contacts). Safe to run every boot.
    try
    {
        await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS RemedyRequests (
  Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  Name TEXT NULL, Contact TEXT NULL, Area VARCHAR(120) NULL,
  Message TEXT NULL, BirthInfo TEXT NULL,
  Handled TINYINT(1) NOT NULL DEFAULT 0,
  CreatedAt DATETIME(6) NOT NULL
) CHARACTER SET=utf8mb4;");
        await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS QuerentCharts (
  Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  Name TEXT NULL, Gender VARCHAR(20) NULL,
  BirthDate TEXT NULL, BirthTime TEXT NULL, TimeZone VARCHAR(80) NULL,
  Location TEXT NULL, NayNan INT NOT NULL DEFAULT 0,
  Consent TINYINT(1) NOT NULL DEFAULT 0,
  CreatedAt DATETIME(6) NOT NULL
) CHARACTER SET=utf8mb4;");
        await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS PdfRequests (
  Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  Email TEXT NULL, Name TEXT NULL, BirthInfo TEXT NULL,
  ApprovalStatus VARCHAR(20) NOT NULL DEFAULT 'Pending',
  DownloadToken VARCHAR(140) NULL, TokenExpiry DATETIME(6) NULL,
  CreatedAt DATETIME(6) NOT NULL
) CHARACTER SET=utf8mb4;");
        await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS Customers (
  Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  Email VARCHAR(200) NOT NULL,
  Username VARCHAR(100) NOT NULL,
  PasswordHash TEXT NOT NULL,
  EmailConfirmed TINYINT(1) NOT NULL DEFAULT 0,
  IsSuspended TINYINT(1) NOT NULL DEFAULT 0,
  VerifyToken VARCHAR(140) NULL, VerifyExpiry DATETIME(6) NULL,
  Gender VARCHAR(20) NULL,
  Dob TEXT NULL, BirthTime TEXT NULL, LocationName TEXT NULL,
  Latitude DOUBLE NULL, Longitude DOUBLE NULL, Timezone VARCHAR(80) NULL,
  ProfileLastUpdated DATETIME(6) NULL,
  CreatedAt DATETIME(6) NOT NULL, UpdatedAt DATETIME(6) NOT NULL,
  UNIQUE KEY uq_customer_email (Email)
) CHARACTER SET=utf8mb4;");
        await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS CustomerCharts (
  Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  CustomerId INT NOT NULL,
  Name TEXT NULL, Gender VARCHAR(20) NULL,
  BirthDate TEXT NULL, BirthTime TEXT NULL, TimeZone VARCHAR(80) NULL,
  Location TEXT NULL, NayNan INT NOT NULL DEFAULT 0,
  CreatedAt DATETIME(6) NOT NULL,
  KEY ix_customerchart_owner (CustomerId)
) CHARACTER SET=utf8mb4;");
        await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS AiReadings (
  Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  CustomerId INT NOT NULL,
  Title TEXT NULL,
  Markdown MEDIUMTEXT NULL,
  Model VARCHAR(60) NULL,
  CreatedAt DATETIME(6) NOT NULL,
  KEY ix_aireading_owner (CustomerId)
) CHARACTER SET=utf8mb4;");
        await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS ResearchPredictions (
  Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  CustomerId INT NOT NULL,
  CreatedAt VARCHAR(40) NOT NULL,
  WindowStart VARCHAR(20) NOT NULL,
  WindowEnd VARCHAR(20) NOT NULL,
  Area VARCHAR(120) NULL,
  Claim TEXT NULL,
  Falsifier TEXT NULL,
  BaseRate DOUBLE NOT NULL DEFAULT 0,
  BaseRateSource VARCHAR(255) NULL,
  Intensity INT NOT NULL DEFAULT 3,
  Valence VARCHAR(20) NOT NULL DEFAULT 'mixed',
  Hash VARCHAR(80) NULL,
  Outcome VARCHAR(20) NULL,
  ReviewedAt VARCHAR(40) NULL,
  Note TEXT NULL,
  RowCreatedAt DATETIME(6) NOT NULL,
  KEY ix_researchpred_owner (CustomerId)
) CHARACTER SET=utf8mb4;");
        await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS ResearchJournalEntries (
  Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  CustomerId INT NOT NULL,
  Month VARCHAR(10) NOT NULL,
  Category VARCHAR(120) NULL,
  Description TEXT NULL,
  Magnitude INT NOT NULL DEFAULT 2,
  CreatedAt VARCHAR(40) NOT NULL,
  RowCreatedAt DATETIME(6) NOT NULL,
  KEY ix_researchjourn_owner (CustomerId)
) CHARACTER SET=utf8mb4;");
        await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS ReadingRequests (
  Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  CustomerId INT NULL,
  QuerentHash CHAR(64) NOT NULL,
  QuerentName TEXT NULL,
  PayloadJson MEDIUMTEXT NULL,
  Status VARCHAR(20) NOT NULL DEFAULT 'Pending',
  Markdown MEDIUMTEXT NULL,
  Model VARCHAR(60) NULL,
  PdfRequested TINYINT(1) NOT NULL DEFAULT 0,
  PdfSent TINYINT(1) NOT NULL DEFAULT 0,
  ClientEmail TEXT NULL,
  CreatedAt DATETIME(6) NOT NULL,
  ApprovedAt DATETIME(6) NULL,
  KEY ix_readingreq_hash (QuerentHash),
  KEY ix_readingreq_status (Status)
) CHARACTER SET=utf8mb4;");
        await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS ConsultationMessages (
  Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  CustomerId INT NOT NULL,
  SenderRole VARCHAR(20) NOT NULL,
  MessageText TEXT NOT NULL,
  CreatedAt DATETIME(6) NOT NULL,
  IsRead TINYINT(1) NOT NULL DEFAULT 0,
  KEY ix_consult_customer (CustomerId)
) CHARACTER SET=utf8mb4;");
        // Additive columns (idempotent — ignore "column exists" on already-migrated DBs).
        foreach (var alter in new[]
        {
            "ALTER TABLE RemedyRequests ADD COLUMN Status VARCHAR(20) NOT NULL DEFAULT 'Pending'",
            "ALTER TABLE RemedyRequests ADD COLUMN Notes TEXT NULL",
            "ALTER TABLE ReadingRequests ADD COLUMN PdfSent TINYINT(1) NOT NULL DEFAULT 0",
            "ALTER TABLE ReadingRequests ADD COLUMN ClientEmail TEXT NULL",
            "ALTER TABLE Customers ADD COLUMN Gender VARCHAR(20) NULL",
            "ALTER TABLE Customers ADD COLUMN Dob TEXT NULL",
            "ALTER TABLE Customers ADD COLUMN BirthTime TEXT NULL",
            "ALTER TABLE Customers ADD COLUMN LocationName TEXT NULL",
            "ALTER TABLE Customers ADD COLUMN Latitude DOUBLE NULL",
            "ALTER TABLE Customers ADD COLUMN Longitude DOUBLE NULL",
            "ALTER TABLE Customers ADD COLUMN Timezone VARCHAR(80) NULL",
            "ALTER TABLE Customers ADD COLUMN IsSuspended TINYINT(1) NOT NULL DEFAULT 0",
            "ALTER TABLE Customers ADD COLUMN ProfileLastUpdated DATETIME(6) NULL",
        })
        {
            try { await db.Database.ExecuteSqlRawAsync(alter); } catch { /* column already present */ }
        }
        logger.LogInformation("Astrology tables ensured (RemedyRequests, QuerentCharts, PdfRequests, Customers, CustomerCharts, AiReadings, ResearchPredictions, ResearchJournalEntries, ReadingRequests).");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Could not ensure astrology tables (non-fatal).");
    }
}

// ─────────────────────────────────────────────────────────────
// 11. ADMIN BOOTSTRAP (opt-in, idempotent) — create OR reset the admin account
//     from environment variables so access is ALWAYS recoverable without DB
//     surgery. Runs ONLY when ADMIN_EMAIL + ADMIN_PASSWORD are set. Once you're
//     back in you may delete the env vars (leaving them simply re-asserts the
//     same password on each boot). The raw password is never logged.
//       Render → Environment:  ADMIN_EMAIL, ADMIN_PASSWORD, ADMIN_USERNAME (opt).
// ─────────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var cfg    = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    var email    = cfg["ADMIN_EMAIL"]?.Trim().ToLowerInvariant();
    var password = cfg["ADMIN_PASSWORD"];
    var username = cfg["ADMIN_USERNAME"]?.Trim();

    if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password))
    {
        try
        {
            if (password.Length < 8)
            {
                logger.LogWarning("Admin bootstrap skipped: ADMIN_PASSWORD must be at least 8 characters.");
            }
            else
            {
                var db   = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var hash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
                var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);

                if (user is null)
                {
                    // Username carries a UNIQUE index — choose a free one.
                    var baseName = string.IsNullOrWhiteSpace(username) ? email.Split('@')[0] : username;
                    if (baseName.Length > 100) baseName = baseName[..100];
                    var uname = baseName;
                    var n = 1;
                    while (await db.Users.AnyAsync(u => u.Username == uname))
                        uname = $"{baseName}{n++}";

                    db.Users.Add(new User
                    {
                        Username     = uname,
                        Email        = email,
                        PasswordHash = hash,
                        Role         = "Admin",
                        CreatedAt    = DateTime.UtcNow,
                        UpdatedAt    = DateTime.UtcNow,
                    });
                    await db.SaveChangesAsync();
                    logger.LogWarning("Admin bootstrap: CREATED admin account for {Email}.", email);
                }
                else
                {
                    user.PasswordHash = hash;
                    user.Role         = "Admin";
                    user.UpdatedAt    = DateTime.UtcNow;
                    // Rename only if a distinct, still-free username was supplied.
                    if (!string.IsNullOrWhiteSpace(username) &&
                        !await db.Users.AnyAsync(u => u.Username == username && u.Id != user.Id))
                        user.Username = username.Length > 100 ? username[..100] : username;
                    await db.SaveChangesAsync();
                    logger.LogWarning("Admin bootstrap: RESET password and ensured Admin role for {Email}.", email);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Admin bootstrap failed (non-fatal); the app continues to run.");
        }
    }
}

app.Run();