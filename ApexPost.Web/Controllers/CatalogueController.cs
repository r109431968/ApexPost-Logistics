using ApexPost.Web.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

public sealed class CatalogueController(ICatalogueService catalogue) : ApexController
{
    [BranchScoped]
    public async Task<IActionResult> Index(
        string? branch, ProductCategory? category, string? search, CancellationToken cancellationToken)
    {
        var code = ResolveBranch(branch);
        var rows = await catalogue.ListAsync(code, category, search, cancellationToken);

        return View(new CatalogueViewModel
        {
            BranchCode = code.Value,
            Rows = rows,
            Category = category,
            Search = search,
            CanAdjust = User.ApexRole == StaffRole.BranchManager && code == HomeBranch
        });
    }

    [HttpPost]
    [Authorize(Policy = AuthPolicies.BranchManagerOnly)]
    [Audit("StockAdjustment")]
    [OnInvalidModel(nameof(Index))]
    public async Task<IActionResult> Adjust(StockAdjustmentViewModel model, CancellationToken cancellationToken)
    {
        var item = await catalogue.AdjustStockAsync(
            StaffId, new Sku(model.Sku), model.Delta, model.Reason, cancellationToken);

        AuditNotes.Add(HttpContext, "sku", item.Sku.Value);
        AuditNotes.Add(HttpContext, "delta", model.Delta.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AuditNotes.Add(HttpContext, "newQuantity", item.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture));

        Success($"Stock for {item.Sku} is now {item.Quantity}.");
        return RedirectToAction(nameof(Index));
    }
}
