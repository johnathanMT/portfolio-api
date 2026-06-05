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

    public ExceptionMiddleware(
        RequestDelegate next,
        ILogger<ExceptionMiddleware> logger,
        IHostEnvironment env)
    {
        _next   = next;
        _logger = logger;
        _env    = env;
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
            // Only expose stack traces in development
            errors: _env.IsDevelopment()
                    ? new List<string> { exception.ToString() }
                    : null
        );

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
    }
}
