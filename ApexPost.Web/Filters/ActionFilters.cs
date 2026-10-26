using System.Diagnostics;
using ApexPost.Web.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace ApexPost.Web.Filters;

/// <summary>
/// Writes who did what to the append-only audit store. It is a filter (not middleware) because it needs MVC
/// knowledge that middleware does not have: the controller and action, the [Audit] marker, route values and
/// the type of result. It never reads action arguments, so passwords and full document numbers cannot leak in.
/// </summary>
public sealed class AuditActionFilter(
    IAuditStore store, IClock clock, ILogger<AuditActionFilter> logger) : IAsyncActionFilter
{
    private static readonly string[] SkippedRouteKeys = ["controller", "action", "area"];

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var marker = context.ActionDescriptor.EndpointMetadata.OfType<AuditAttribute>().FirstOrDefault();
        if (marker is null)
        {
            await next();
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        ActionExecutedContext? executed = null;

        try
        {
            executed = await next();
        }
        finally
        {
            stopwatch.Stop();
            await TryWriteAsync(context, executed, marker, stopwatch.ElapsedMilliseconds);
        }
    }

    private async Task TryWriteAsync(
        ActionExecutingContext context, ActionExecutedContext? executed, AuditAttribute marker, long elapsedMs)
    {
        try
        {
            var http = context.HttpContext;
            var details = new Dictionary<string, string>(AuditNotes.Read(http));

            var failure = executed?.Exception;
            if (failure is not null)
                details["error"] = failure.GetType().Name;

            var actor = AuditNotes.ReadActor(http);
            var entry = new AuditEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = clock.UtcNow,
                StaffId = http.User.StaffId ?? actor?.StaffId,
                Role = http.User.ApexRole?.ToString() ?? actor?.Role.ToString(),
                BranchCode = http.User.HomeBranchCode ?? actor?.BranchCode,
                Controller = context.RouteData.Values["controller"]?.ToString() ?? "unknown",
                Action = context.RouteData.Values["action"]?.ToString() ?? "unknown",
                Operation = marker.Operation,
                RouteValues = context.RouteData.Values
                    .Where(pair => !SkippedRouteKeys.Contains(pair.Key, StringComparer.OrdinalIgnoreCase) && pair.Value is not null)
                    .ToDictionary(pair => pair.Key, pair => LogSanitiser.Clean(pair.Value!.ToString(), 100)),
                Details = details,
                StatusCode = StatusOf(executed, http),
                DurationMs = elapsedMs,
                CorrelationId = http.CorrelationId
            };

            // The write must finish even if the browser disconnects, so it uses its own short timeout.
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await store.AppendAsync(entry, timeout.Token);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Auditing must never break the business action, but a failure must be visible.
            logger.LogError(ex, "The audit entry could not be written.");
        }
    }

    private static int StatusOf(ActionExecutedContext? executed, HttpContext http)
    {
        if (executed?.Exception is { } exception)
            return ProblemMapping.TryMap(exception, out var status, out _, out _)
                ? status
                : StatusCodes.Status500InternalServerError;

        return executed?.Result switch
        {
            IStatusCodeActionResult { StatusCode: { } code } => code,
            RedirectToActionResult or RedirectToRouteResult or RedirectResult or LocalRedirectResult
                => StatusCodes.Status302Found,
            ViewResult => StatusCodes.Status200OK,
            _ => http.Response.StatusCode
        };
    }
}

/// <summary>
/// One consistent answer when the model is invalid: a ValidationProblem for JSON callers, an error message and
/// redirect for forms marked [OnInvalidModel], and the same view redisplayed for everything else.
/// </summary>
public sealed class ValidationFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.ModelState.IsValid)
            return;

        var request = context.HttpContext.Request;
        var wantsJson = request.Path.StartsWithSegments("/integration")
            || request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase);

        if (wantsJson)
        {
            context.Result = new BadRequestObjectResult(new ValidationProblemDetails(context.ModelState)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed"
            });
            return;
        }

        var redirect = context.ActionDescriptor.EndpointMetadata.OfType<OnInvalidModelAttribute>().FirstOrDefault();
        if (redirect is not null && context.Controller is Controller redirecting)
        {
            var messages = context.ModelState.Values
                .SelectMany(entry => entry.Errors)
                .Select(error => error.ErrorMessage)
                .Where(message => !string.IsNullOrWhiteSpace(message));

            redirecting.TempData["Error"] = string.Join(" ", messages);
            context.Result = new RedirectToActionResult(
                redirect.Action,
                redirect.Controller,
                context.RouteData.Values.ContainsKey("id") ? new { id = context.RouteData.Values["id"] } : null);
            return;
        }

        if (context.Controller is Controller controller)
        {
            var model = context.ActionArguments.Values.FirstOrDefault(
                argument => argument is not null && !argument.GetType().IsPrimitive && argument is not string);

            if (model is not null)
                controller.ViewData.Model = model;

            context.Result = new ViewResult { ViewData = controller.ViewData, TempData = controller.TempData };
            return;
        }

        context.Result = new BadRequestObjectResult(new ValidationProblemDetails(context.ModelState));
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}

/// <summary>Logs a warning when an action is slower than Performance:SlowActionMs.</summary>
public sealed class PerformanceFilter(PerformanceSettings settings, ILogger<PerformanceFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await next();
        }
        finally
        {
            stopwatch.Stop();
            if (stopwatch.ElapsedMilliseconds > settings.SlowActionMs)
                logger.LogWarning(
                    "Slow action {Action} took {ElapsedMs} ms (limit {LimitMs} ms)",
                    context.ActionDescriptor.DisplayName, stopwatch.ElapsedMilliseconds, settings.SlowActionMs);
        }
    }
}

/// <summary>
/// Turns domain and validation exceptions into friendly answers. A failed form POST goes back to the page it
/// came from with a message (Post/Redirect/Get). Anything else becomes Problem Details with the right status.
/// Unknown exceptions are left alone for the global exception middleware.
/// </summary>
public sealed class DomainExceptionFilter(ILogger<DomainExceptionFilter> logger) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (!ProblemMapping.TryMap(context.Exception, out var status, out var title, out var message))
            return;

        logger.LogInformation("Business error {Status}: {Message}", status, message);

        var request = context.HttpContext.Request;
        var isForm = HttpMethods.IsPost(request.Method) && context.ActionDescriptor is ControllerActionDescriptor;

        if (isForm)
        {
            var tempData = context.HttpContext.RequestServices
                .GetRequiredService<ITempDataDictionaryFactory>().GetTempData(context.HttpContext);
            tempData["Error"] = message;

            var referer = request.Headers.Referer.ToString();
            var target = Uri.TryCreate(referer, UriKind.Absolute, out var uri) && uri.Host == request.Host.Host
                ? uri.PathAndQuery
                : "/";

            context.Result = new LocalRedirectResult(target);
        }
        else
        {
            var problem = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = message,
                Type = $"https://httpstatuses.com/{status}"
            };
            problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
            problem.Extensions["correlationId"] = context.HttpContext.CorrelationId;

            context.Result = new ObjectResult(problem) { StatusCode = status };
            //context.Result = new ObjectResult(new ProblemDetails { Status = status, Title = title, Detail = message })
            //{
            //    StatusCode = status
            //};
        }

        context.ExceptionHandled = true;
    }
}

/// <summary>Blocks a request for another branch's data. Auditors and administrators may look at any branch.</summary>
public sealed class BranchAccessFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        var user = context.HttpContext.User;
        if (user.CanSeeAllBranches)
            return;

        var requested = context.ActionArguments.TryGetValue("branch", out var argument)
            ? argument?.ToString()
            : context.HttpContext.Request.Query["branch"].ToString();

        if (!string.IsNullOrWhiteSpace(requested)
            && !string.Equals(requested.Trim(), user.HomeBranchCode, StringComparison.OrdinalIgnoreCase))
            context.Result = new ForbidResult();
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}

public sealed class BranchScopedAttribute() : TypeFilterAttribute(typeof(BranchAccessFilter));
