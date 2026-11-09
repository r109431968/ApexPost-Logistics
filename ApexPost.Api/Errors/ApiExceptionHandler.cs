using ApexPost.Application.Exceptions;
using ApexPost.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Api.Errors;

/// <summary>
/// The one place that turns an exception into an HTTP answer. Every error leaves the API as ProblemDetails
/// (application/problem+json). Stack traces are only included in Development.
/// </summary>
public sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetails,
    IHostEnvironment environment,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // The caller hung up. Nobody is listening, and it is not a server error.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            httpContext.Response.StatusCode = 499;
            return true;
        }

        var (status, title) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "The resource was not found"),
            ParcelConflictException or ConcurrencyConflictException or DuplicateValueException
                => (StatusCodes.Status409Conflict, "The request conflicts with the current state"),
            DomainException => (StatusCodes.Status400BadRequest, "A business rule was broken"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled exception on {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        else
            logger.LogInformation("Handled {Status} on {Method} {Path}: {Message}", status, httpContext.Request.Method, httpContext.Request.Path, exception.Message);

        httpContext.Response.StatusCode = status;

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = status switch
            {
                StatusCodes.Status500InternalServerError when environment.IsDevelopment() => exception.ToString(),
                StatusCodes.Status500InternalServerError => "The server could not complete the request. Quote the traceId if you contact support.",
                _ => exception.Message
            },
            Instance = $"{httpContext.Request.Method} {httpContext.Request.Path}"
        };

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        });
    }
}
