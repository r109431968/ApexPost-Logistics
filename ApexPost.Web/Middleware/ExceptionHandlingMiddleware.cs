namespace ApexPost.Web.Middleware;

/// <summary>
/// Last line of defence. Returns Problem Details with a safe message, a trace ID and the correlation ID.
/// Stack traces are only included in Development.
/// </summary>
public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The caller went away. That is not a failure of ours, so there is nothing to report.
            logger.LogInformation("Request was cancelled by the client.");
            if (!context.Response.HasStarted)
                context.Response.StatusCode = 499;
        }
        catch (Exception exception)
        {
            var known = ProblemMapping.TryMap(exception, out var status, out var title, out var message);

            if (known)
                logger.LogWarning("Request rejected with {Status}: {Message}", status, exception.Message);
            else
                logger.LogError(exception, "Unhandled exception while processing the request.");

            if (context.Response.HasStarted)
            {
                logger.LogWarning("The response had already started, so the error page could not be sent.");
                return;
            }

            context.Response.Clear();
            var detail = environment.IsDevelopment() && !known ? exception.ToString() : message;
            await ProblemWriter.WriteAsync(context, status, title, detail);
        }
    }
}
