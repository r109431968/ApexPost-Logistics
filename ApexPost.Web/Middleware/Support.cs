using System.Text;
using ApexPost.Application.Exceptions;
using ApexPost.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Middleware;

public static class CorrelationKeys
{
    public const string Header = "X-Correlation-Id";
    public const string ItemKey = "ApexPost.CorrelationId";
}

public static class HttpContextApexExtensions
{
    extension(HttpContext context)
    {
        /// <summary>The correlation ID of the current request (falls back to the trace identifier).</summary>
        public string CorrelationId =>
            context.Items.TryGetValue(CorrelationKeys.ItemKey, out var value) && value is string id
                ? id
                : context.TraceIdentifier;
    }
}

/// <summary>Removes line breaks and control characters so user input cannot forge log lines.</summary>
public static class LogSanitiser
{
    public static string Clean(string? value, int maxLength = 200)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var builder = new StringBuilder(Math.Min(value.Length, maxLength));
        foreach (var character in value)
        {
            if (builder.Length >= maxLength)
                break;
            builder.Append(char.IsControl(character) ? '_' : character);
        }

        return builder.ToString();
    }
}

public static class ProblemWriter
{
    public static async Task WriteAsync(
        HttpContext context, int status, string title, string? detail = null,
        IDictionary<string, object?>? extensions = null)
    {
        if (context.Response.HasStarted)
            return;

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Type = $"https://httpstatuses.com/{status}"
        };

        problem.Extensions["traceId"] = context.TraceIdentifier;
        problem.Extensions["correlationId"] = context.CorrelationId;

        if (extensions is not null)
            foreach (var pair in extensions)
                problem.Extensions[pair.Key] = pair.Value;

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(
            problem, options: null, contentType: "application/problem+json", cancellationToken: context.RequestAborted);
    }
}

/// <summary>One place that decides which exception becomes which HTTP status. Used by middleware and the exception filter.</summary>
public static class ProblemMapping
{
    public static bool TryMap(Exception exception, out int status, out string title, out string message)
    {
        switch (exception)
        {
            case ValidationFailedException validation:
                (status, title, message) = (StatusCodes.Status400BadRequest, "Validation failed", validation.Message);
                return true;
            case NotFoundException notFound:
                (status, title, message) = (StatusCodes.Status404NotFound, "Not found", notFound.Message);
                return true;
            case InsufficientStockException stock:
                (status, title, message) = (StatusCodes.Status409Conflict, "Insufficient stock", stock.Message);
                return true;
            case DomainException domain:
                (status, title, message) = (StatusCodes.Status422UnprocessableEntity, "Business rule violated", domain.Message);
                return true;
            case ExternalServiceTimeoutException timeout:
                (status, title, message) = (StatusCodes.Status504GatewayTimeout, "Upstream timeout", timeout.Message);
                return true;
            case ExternalServiceException external:
                (status, title, message) = (StatusCodes.Status502BadGateway, "Upstream failure", external.Message);
                return true;
            case ArgumentException argument:
                (status, title, message) = (StatusCodes.Status400BadRequest, "Invalid value", argument.Message);
                return true;
            default:
                (status, title, message) = (StatusCodes.Status500InternalServerError, "Unexpected error",
                    "Something went wrong. Please quote the trace ID if you contact support.");
                return false;
        }
    }
}
