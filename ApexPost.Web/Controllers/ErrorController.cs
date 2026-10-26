using ApexPost.Web.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

[AllowAnonymous]
public sealed class ErrorController : Controller
{
    [Route("Error/Status/{code:int}")]
    public IActionResult Status(int code)
    {
        Response.StatusCode = code;

        var (title, message) = code switch
        {
            403 => ("Access denied", "Your role does not allow this action."),
            404 => ("Page not found", "The page you asked for does not exist."),
            429 => ("Too many requests", "Please wait a moment and try again."),
            >= 500 => ("Something went wrong", "The problem has been logged. Quote the trace ID if you contact support."),
            _ => ("Request failed", "The request could not be completed.")
        };

        return View(new ErrorViewModel
        {
            Code = code,
            Title = title,
            Message = message,
            TraceId = HttpContext.TraceIdentifier,
            CorrelationId = HttpContext.CorrelationId
        });
    }
}
