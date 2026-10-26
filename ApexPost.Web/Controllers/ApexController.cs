using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

/// <summary>Shared helpers for controllers that need a signed-in staff member.</summary>
[Authorize]
public abstract class ApexController : Controller
{
    protected Guid StaffId => User.StaffId ?? throw new InvalidOperationException("No signed-in staff member.");

    protected BranchCode HomeBranch =>
        new(User.HomeBranchCode ?? throw new InvalidOperationException("No branch claim."));

    /// <summary>The requested branch, or the user's own branch when nothing was requested.</summary>
    protected BranchCode ResolveBranch(string? requested) =>
        string.IsNullOrWhiteSpace(requested) ? HomeBranch : new BranchCode(requested);

    /// <summary>For reports: auditors and administrators may look at all branches (null), others see only their own.</summary>
    protected BranchCode? ScopeBranch(string? requested) =>
        User.CanSeeAllBranches
            ? (string.IsNullOrWhiteSpace(requested) ? null : new BranchCode(requested))
            : HomeBranch;

    protected void Success(string message) => TempData["Success"] = message;
}
