using ApexPost.Application.Reports;
using ApexPost.Application.Specifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

[Authorize(Policy = AuthPolicies.CanViewReports)]
[Authorize(Policy = AuthPolicies.SameBranchOnly)]
public sealed class ReportsController(IReportService reports, IClock clock) : ApexController
{
    public IActionResult Index() => View();

    public async Task<IActionResult> DailySales(string? branch, DateOnly? day, CancellationToken cancellationToken)
    {
        var code = ResolveBranch(branch);
        var chosenDay = day ?? TransactionSpecs.DayOf(clock.UtcNow);

        return View(new DailySalesViewModel
        {
            Filter = FilterFor(code),
            Report = await reports.DailySalesAsync(code, chosenDay, cancellationToken)
        });
    }

    public async Task<IActionResult> LowStock(string? branch, int? threshold, CancellationToken cancellationToken)
    {
        var code = ResolveBranch(branch);

        return View(new LowStockViewModel
        {
            Filter = FilterFor(code),
            Threshold = threshold,
            Items = await reports.LowStockAsync(code, threshold, cancellationToken)
        });
    }

    public async Task<IActionResult> ShippingVolume(
        string? branch, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        var scope = ScopeBranch(branch);
        var toDay = to ?? TransactionSpecs.DayOf(clock.UtcNow);
        var fromDay = from ?? toDay.AddDays(-30);

        return View(new ShippingVolumeViewModel
        {
            Filter = FilterFor(scope),
            From = fromDay,
            To = toDay,
            Rows = await reports.ShippingVolumeAsync(fromDay, toDay, scope, cancellationToken)
        });
    }

    /// <summary>The auditor view: money orders and identity verifications per branch (masked numbers only).</summary>
    public async Task<IActionResult> MoneyOrders(string? branch, CancellationToken cancellationToken)
    {
        var scope = ScopeBranch(branch);

        return View(new MoneyOrderAuditViewModel
        {
            Filter = FilterFor(scope),
            Summaries = await reports.MoneyOrderAuditAsync(scope, cancellationToken)
        });
    }

    private ReportFilterViewModel FilterFor(BranchCode? branch) => new()
    {
        BranchCode = branch?.Value ?? string.Empty,
        CanChooseBranch = User.CanSeeAllBranches
    };
}
