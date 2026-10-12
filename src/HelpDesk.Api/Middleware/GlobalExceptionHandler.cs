using HelpDesk.Api.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace HelpDesk.Api.Middleware;

/// <summary>
/// Turns exceptions into consistent JSON error responses. Unexpected errors
/// are logged with a reference id the user can quote to support, while the
/// technical details stay in the log instead of the browser.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title, detail) = exception switch
        {
            NotFoundException e     => (StatusCodes.Status404NotFound, "Not found", e.Message),
            BusinessRuleException e => (StatusCodes.Status400BadRequest, "Request not allowed", e.Message),
            _                       => (StatusCodes.Status500InternalServerError, "Unexpected error",
                                        $"Something went wrong. Quote reference {context.TraceIdentifier} to support.")
        };

        if (status == StatusCodes.Status500InternalServerError)
            _logger.LogError(exception, "Unhandled error. Reference {TraceId}", context.TraceIdentifier);
        else
            _logger.LogWarning("{Title}: {Detail}", title, detail);

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        }, ct);
        return true;
    }
}
