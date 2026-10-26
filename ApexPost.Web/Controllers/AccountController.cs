using System.Security.Claims;
using ApexPost.Web.Filters;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ApexPost.Web.Controllers;


public sealed class AccountController(ILoginService login, ILogger<AccountController> logger) : Controller
{
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null) =>
        User.Identity?.IsAuthenticated == true
            ? RedirectToAction("Index", "Dashboard")
            : View(new LoginViewModel { ReturnUrl = returnUrl });

    // Strict rate limit against password guessing. The limiter runs as middleware, before this action.
    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [Audit("Login")]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        var staff = await login.AuthenticateAsync(model.Email, model.Password, cancellationToken);

        if (staff is null)
        {
            // One message for every failure, so the page does not reveal which emails exist.
            AuditNotes.Add(HttpContext, "outcome", "failed");
            logger.LogWarning("A sign-in attempt failed.");
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            model.Password = string.Empty;
            return View(model);
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(ApexClaims.StaffId, staff.Id.ToString()),
                new Claim(ClaimTypes.Name, staff.Name),
                new Claim(ApexClaims.Role, staff.Role.ToString()),
                new Claim(ApexClaims.BranchCode, staff.HomeBranch.Value)
            ],
            AuthPolicies.CookieScheme,
            ClaimTypes.Name,
            ApexClaims.Role);

        await HttpContext.SignInAsync(AuthPolicies.CookieScheme, new ClaimsPrincipal(identity));

        AuditNotes.SetActor(HttpContext, staff.Id, staff.Role, staff.HomeBranch.Value);
        AuditNotes.Add(HttpContext, "outcome", "success");

        // Post/Redirect/Get: a refresh cannot post the password again.
        return !string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl)
            ? LocalRedirect(model.ReturnUrl)
            : RedirectToAction("Index", "Dashboard");
    }

    [HttpPost]
    [Authorize]
    [Audit("Logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(AuthPolicies.CookieScheme);
        return RedirectToAction(nameof(Login));
    }
}
