using System.Diagnostics;

namespace ApexPost.Web.Middleware;

/// <summary>
/// Structured request log plus timing. The log line is written in a finally block, so a request that fails
/// or is cancelled is still timed.
/// </summary>
public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Response-Time-Ms"] =
                stopwatch.ElapsedMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return Task.CompletedTask;
        });

        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();
            logger.LogInformation(
                "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs} ms",
                context.Request.Method,
                LogSanitiser.Clean(context.Request.Path.Value),
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds);
        }
    }
}
