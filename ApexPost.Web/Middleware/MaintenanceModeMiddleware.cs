using Microsoft.Extensions.Options;

namespace ApexPost.Web.Middleware;

/// <summary>
/// When Maintenance:Enabled is true every request gets 503 with Retry-After, except /health so monitors still work.
/// IOptionsMonitor means the switch can be flipped without restarting the app.
/// </summary>
public sealed class MaintenanceModeMiddleware(RequestDelegate next, IOptionsMonitor<MaintenanceOptions> options)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var current = options.CurrentValue;

        if (current.Enabled && !context.Request.Path.StartsWithSegments("/health"))
        {
            context.Response.Headers.RetryAfter =
                current.RetryAfterSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
            await ProblemWriter.WriteAsync(
                context, StatusCodes.Status503ServiceUnavailable,
                "Down for maintenance", "ApexPost is being updated. Please try again shortly.");
            return;
        }

        await next(context);
    }
}
