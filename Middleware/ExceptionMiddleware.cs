using System.Net;
using System.Text.Json;
using PortfolioApi.Common;

namespace PortfolioApi.Middleware;

/// <summary>
/// Global exception handler — catches any unhandled exception and returns
/// a consistent JSON error response instead of leaking stack traces to clients.
/// </summary>
public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;
    private readonly bool _detailedErrors;

    public ExceptionMiddleware(
        RequestDelegate next,
        ILogger<ExceptionMiddleware> logger,
        IHostEnvironment env,
        IConfiguration config)
    {
        _next   = next;
        _logger = logger;
        _env    = env;
        // Set DetailedErrors=true (appsettings or env var) on Render to temporarily
        // expose the real exception text in the response `errors[]` for debugging
        // (e.g. a MySqlException about a missing ArticleImages table). Turn it off
        // again once diagnosed so internals aren't leaked to clients.
        _detailedErrors = _env.IsDevelopment()
            || string.Equals(config["DetailedErrors"], "true", StringComparison.OrdinalIgnoreCase);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unhandled exception on {Method} {Path}",
                context.Request.Method,
                context.Request.Path);

            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, message) = exception switch
        {
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized,     "You are not authorised to perform this action."),
            InvalidOperationException   => (HttpStatusCode.BadRequest,       exception.Message),
            KeyNotFoundException        => (HttpStatusCode.NotFound,         exception.Message),
            ArgumentException           => (HttpStatusCode.BadRequest,       exception.Message),
            _                           => (HttpStatusCode.InternalServerError, "An unexpected error occurred.")
        };

        context.Response.StatusCode = (int)statusCode;

        var response = ApiResponse<object>.Fail(
            message:    message,
            statusCode: (int)statusCode,
            // Expose the real exception text only when detailed errors are enabled
            // (Development, or DetailedErrors=true). Includes inner exception, which
            // is where the actual DB/Cloudinary cause usually lives.
            errors: _detailedErrors
                    ? new List<string> { exception.ToString() }
                    : null
        );

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
    }
}
