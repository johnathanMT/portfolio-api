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
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PortfolioApi.Data;
using PortfolioApi.Interfaces;
using PortfolioApi.Middleware;
using PortfolioApi.Repositories;
using PortfolioApi.Services;
using PortfolioApi.Validators;

var builder = WebApplication.CreateBuilder(args);

// ─────────────────────────────────────────────────────────────
// 1. DATABASE  — Pomelo MySQL with Aiven connection string
// ─────────────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(
        connectionString,
        ServerVersion.AutoDetect(connectionString),
        mySqlOptions =>
        {
            mySqlOptions.EnableRetryOnFailure(
                maxRetryCount:                    5,
                maxRetryDelay:                    TimeSpan.FromSeconds(10),
                errorNumbersToAdd:                null);
            mySqlOptions.CommandTimeout(30);
        }));

// ─────────────────────────────────────────────────────────────
// 2. DEPENDENCY INJECTION — Repositories & Services
// ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<IUserRepository,        UserRepository>();
builder.Services.AddScoped<IArticleRepository,     ArticleRepository>();
builder.Services.AddScoped<IInteractionRepository, InteractionRepository>();
builder.Services.AddScoped<IAuthService,           AuthService>();
builder.Services.AddScoped<IArticleService,        ArticleService>();
builder.Services.AddScoped<IInteractionService,    InteractionService>();
builder.Services.AddSingleton<IImageService,       CloudinaryImageService>();

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
        options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew                = TimeSpan.Zero, // No tolerance for expired tokens
        };

        // Return JSON on 401/403 instead of an empty body
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async ctx =>
            {
                ctx.HandleResponse();
                ctx.Response.StatusCode  = 401;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync(
                    """{"success":false,"message":"Authentication required. Please provide a valid JWT token.","statusCode":401}""");
            },
            OnForbidden = async ctx =>
            {
                ctx.Response.StatusCode  = 403;
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
              .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
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

    // General API policy: 100 req / 60 s
    options.AddFixedWindowLimiter("general", opt =>
    {
        opt.Window             = TimeSpan.FromSeconds(rlSection.GetValue("GeneralWindowSeconds", 60));
        opt.PermitLimit        = rlSection.GetValue("GeneralPermitLimit", 100);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit         = 0;
    });

    // Auth endpoints policy: 10 req / 15 min (brute-force protection)
    options.AddFixedWindowLimiter("auth", opt =>
    {
        opt.Window             = TimeSpan.FromSeconds(rlSection.GetValue("AuthWindowSeconds", 900));
        opt.PermitLimit        = rlSection.GetValue("AuthPermitLimit", 10);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit         = 0;
    });

    // Anonymous interactions policy: 30 req / 60 s (like/react spam protection)
    options.AddFixedWindowLimiter("interactions", opt =>
    {
        opt.Window             = TimeSpan.FromSeconds(rlSection.GetValue("InteractionsWindowSeconds", 60));
        opt.PermitLimit        = rlSection.GetValue("InteractionsPermitLimit", 30);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit         = 0;
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
        Title       = "MTN Portfolio API",
        Version     = "v1",
        Description = "Production-ready REST API for Myo Thant Naing's personal portfolio and blog system.",
        Contact     = new OpenApiContact
        {
            Name  = "Myo Thant Naing",
            Email = "myothantnaing1178@gmail.com",
            Url   = new Uri("https://johnathanmt.github.io/Myweb/"),
        },
    });

    // Add JWT Bearer button to Swagger UI
    var securityScheme = new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Description  = "Enter: **Bearer {your_jwt_token}**",
        In           = ParameterLocation.Header,
        Type         = SecuritySchemeType.Http,
        Scheme       = "bearer",
        BearerFormat = "JWT",
        Reference    = new OpenApiReference
        {
            Id   = JwtBearerDefaults.AuthenticationScheme,
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
    opt.MultipartBodyLengthLimit = 10 * 1024 * 1024; // 10 MB max upload
});

// ─────────────────────────────────────────────────────────────
// BUILD THE APPLICATION
// ─────────────────────────────────────────────────────────────
var app = builder.Build();

// ─────────────────────────────────────────────────────────────
// 9. MIDDLEWARE PIPELINE (order matters)
// ─────────────────────────────────────────────────────────────

// A. Global exception handler — must be FIRST so it catches everything
app.UseMiddleware<ExceptionMiddleware>();

// B. Security headers
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    ctx.Response.Headers.Append("X-Frame-Options",        "DENY");
    ctx.Response.Headers.Append("X-XSS-Protection",       "1; mode=block");
    ctx.Response.Headers.Append("Referrer-Policy",         "strict-origin-when-cross-origin");
    ctx.Response.Headers.Append("Permissions-Policy",      "camera=(), microphone=(), geolocation=()");
    await next();
});

// C. Swagger
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "MTN Portfolio API v1");
    c.RoutePrefix    = string.Empty; // Swagger at root /
    c.DocumentTitle  = "MTN Portfolio API";
    c.DefaultModelsExpandDepth(-1); 
});

// D. HTTPS redirection
app.UseHttpsRedirection();

// E. Rate limiter
app.UseRateLimiter();

// F. CORS — must be before Auth
app.UseCors("PortfolioCors");

// G. Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// H. Controllers
app.MapControllers();

// ─────────────────────────────────────────────────────────────
// 10. AUTO-MIGRATE ON STARTUP (safe for Render cold starts)
// ─────────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db     = scope.ServiceProvider.GetRequiredService<AppDbContext>();
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
        logger.LogError(ex, "Failed to apply migrations. Check connection string / Aiven IP access.");
        throw;
    }
}

app.Run();