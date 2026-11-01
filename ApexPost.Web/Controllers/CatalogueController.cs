using ApexPost.Web.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

public sealed class CatalogueController(ICatalogueService catalogue, IPriceRevisionService priceRevision) : ApexController
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
            CanAdjust = User.ApexRole == StaffRole.BranchManager && code == HomeBranch,
            CanRevisePrices = User.ApexRole == StaffRole.Administrator
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

    [HttpPost]
    [Authorize(Policy = AuthPolicies.AdministratorOnly)]
    [Audit("PriceRevision")]
    [OnInvalidModel(nameof(Index))]
    public async Task<IActionResult> RevisePrices(PriceRevisionViewModel model, CancellationToken cancellationToken)
    {
        var category = model.Category!.Value;
        var changed = await priceRevision.ReviseCategoryAsync(category, model.Percent, cancellationToken);

        AuditNotes.Add(HttpContext, "category", category.ToString());
        AuditNotes.Add(HttpContext, "percent", model.Percent.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AuditNotes.Add(HttpContext, "products", changed.ToString(System.Globalization.CultureInfo.InvariantCulture));

        Success($"{changed} prices in {category} changed by {model.Percent}%.");
        return RedirectToAction(nameof(Index));
    }
}
