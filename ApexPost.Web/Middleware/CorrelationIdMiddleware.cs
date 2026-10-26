using System.Text.RegularExpressions;

namespace ApexPost.Web.Middleware;

/// <summary>
/// Reuses an incoming X-Correlation-Id (if it is short and harmless) or creates one, echoes it in the response
/// and adds it to every log line written during the request through a logging scope.
/// </summary>
public sealed partial class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    [GeneratedRegex(@"^[A-Za-z0-9\-_.]{1,64}$")]
    private static partial Regex SafeId();

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[CorrelationKeys.Header].ToString();
        var id = SafeId().IsMatch(incoming) ? incoming : Guid.NewGuid().ToString("N");

        context.Items[CorrelationKeys.ItemKey] = id;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationKeys.Header] = id;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = id }))
        {
            await next(context);
        }
    }
}
