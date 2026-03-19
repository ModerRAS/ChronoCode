using System.Text.Json;
using FluentValidation;

namespace ChronoCode.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var traceId = context.TraceIdentifier;
        var timestamp = DateTime.UtcNow;

        string code;
        string message;
        List<string>? details = null;

        switch (exception)
        {
            case ValidationException validationException:
                code = "VALIDATION_ERROR";
                message = "One or more validation errors occurred.";
                details = validationException.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}").ToList();
                _logger.LogWarning("Validation error: {Details}", string.Join(", ", details));
                break;

            case KeyNotFoundException:
                code = "NOT_FOUND";
                message = "The requested resource was not found.";
                _logger.LogWarning("Resource not found: {Message}", exception.Message);
                break;

            case UnauthorizedAccessException:
                code = "UNAUTHORIZED";
                message = "Access to the requested resource is unauthorized.";
                _logger.LogWarning("Unauthorized access: {Message}", exception.Message);
                break;

            default:
                code = "INTERNAL_ERROR";
                message = "An internal server error occurred.";
                _logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);
                break;
        }

        var errorResponse = new
        {
            error = new
            {
                code,
                message,
                details,
                timestamp = timestamp.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                traceId
            }
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = GetStatusCode(code);

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse, options));
    }

    private static int GetStatusCode(string code)
    {
        return code switch
        {
            "VALIDATION_ERROR" => StatusCodes.Status400BadRequest,
            "NOT_FOUND" => StatusCodes.Status404NotFound,
            "UNAUTHORIZED" => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status500InternalServerError
        };
    }
}

public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ExceptionHandlingMiddleware>();
    }
}