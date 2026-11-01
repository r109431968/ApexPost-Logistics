using ApexPost.Application.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

[Authorize(Policy = AuthPolicies.SameBranchOnly)]
public sealed class DashboardController(
    IReportService reports, IBranchService branches, INotificationSink notifications) : ApexController
{
    public async Task<IActionResult> Index(string? branch, CancellationToken cancellationToken)
    {
        var code = ResolveBranch(branch);

        var found = await branches.GetAsync(code, cancellationToken);
        if (found is null)
            return NotFound();

        // Sales, stock and alerts are built one after another inside the report service (one DbContext per request).
        var dashboard = await reports.DashboardAsync(code, cancellationToken);

        return View(new DashboardViewModel
        {
            Dashboard = dashboard,
            BranchName = found.Name,
            Notifications = notifications.Recent(5)
        });
    }
}
