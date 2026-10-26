using ApexPost.Web.Filters;
using ApexPost.Web.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

[Authorize(Policy = AuthPolicies.AdministratorOnly)]
[IpRestricted(IpZone.Admin)]
public sealed class BranchesController(IBranchService branches) : ApexController
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await branches.ListAsync(cancellationToken));

    [HttpPost]
    [Audit("BranchOpen")]
    public async Task<IActionResult> Open(string code, CancellationToken cancellationToken)
    {
        await branches.SetOpenAsync(new BranchCode(code), true, cancellationToken);
        AuditNotes.Add(HttpContext, "branch", code.Trim().ToUpperInvariant());
        Success($"{code} is open.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Audit("BranchClose")]
    public async Task<IActionResult> Close(string code, CancellationToken cancellationToken)
    {
        await branches.SetOpenAsync(new BranchCode(code), false, cancellationToken);
        AuditNotes.Add(HttpContext, "branch", code.Trim().ToUpperInvariant());
        Success($"{code} is closed.");
        return RedirectToAction(nameof(Index));
    }
}
