using ApexPost.Application.Settings;
using ApexPost.Web.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

/// <summary>
/// The counter screen. Every action is thin: it checks the user may see the transaction, calls one use case
/// and redirects (Post/Redirect/Get). All business rules live in the Domain and Application layers.
/// </summary>
[Authorize(Policy = AuthPolicies.CanTransact)]
public sealed class TransactionsController(
    ICheckoutService checkout,
    IReversalService reversal,
    ITransactionQueries queries,
    ICatalogueService catalogue,
    IBranchService branches,
    IZoneCatalog zones,
    MoneyOrderSettings moneyOrderSettings) : ApexController
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(new TransactionListViewModel
        {
            BranchCode = HomeBranch.Value,
            Transactions = await queries.RecentAsync(HomeBranch, 25, cancellationToken)
        });

    [HttpGet]
    public async Task<IActionResult> New(CancellationToken cancellationToken)
    {
        var branch = await branches.GetAsync(HomeBranch, cancellationToken);
        return branch is null ? NotFound() : View(new NewTransactionViewModel { Branch = branch });
    }

    [HttpPost]
    [Audit("TransactionOpened")]
    public async Task<IActionResult> Open(Guid counterId, CancellationToken cancellationToken)
    {
        var transaction = await checkout.OpenAsync(HomeBranch, counterId, StaffId, cancellationToken);
        AuditNotes.Add(HttpContext, "transactionId", transaction.Id.ToString());
        return RedirectToAction(nameof(Details), new { id = transaction.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var transaction = await queries.GetAsync(id, cancellationToken);
        if (transaction is null)
            return NotFound();
        if (transaction.BranchCode != HomeBranch)
            return Forbid();

        var products = await catalogue.ListAsync(transaction.BranchCode, null, null, cancellationToken);
        var zoneList = await zones.ListAsync(cancellationToken);

        return View(new TransactionDetailsViewModel
        {
            Transaction = transaction,
            Products = products,
            Zones = zoneList,
            VerificationThreshold = moneyOrderSettings.IdentityVerificationThreshold,
            CanReverse = User.ApexRole == StaffRole.BranchManager
                && transaction.Status is TransactionStatus.Open or TransactionStatus.Paid
        });
    }

    [HttpPost]
    [OnInvalidModel(nameof(Details))]
    public async Task<IActionResult> AddItem(Guid id, AddRetailViewModel model, CancellationToken cancellationToken)
    {
        await EnsureOwnBranchAsync(id, cancellationToken);
        await checkout.AddRetailLineAsync(id, new Sku(model.Sku), model.Quantity, cancellationToken);
        Success("Item added.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [OnInvalidModel(nameof(Details))]
    public async Task<IActionResult> AddShipping(Guid id, AddShippingViewModel model, CancellationToken cancellationToken)
    {
        await EnsureOwnBranchAsync(id, cancellationToken);
        await checkout.AddShippingLineAsync(
            id, model.ShipmentType, Weight.FromKilograms(model.WeightKg), model.ZoneCode, model.ServiceLevel,
            cancellationToken);
        Success("Shipping added.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Audit("MoneyOrder")]
    [OnInvalidModel(nameof(Details))]
    public async Task<IActionResult> AddMoneyOrder(Guid id, AddMoneyOrderViewModel model, CancellationToken cancellationToken)
    {
        await EnsureOwnBranchAsync(id, cancellationToken);
        await checkout.AddMoneyOrderAsync(
            id,
            new MoneyOrderRequest { Sender = model.Sender, Recipient = model.Recipient, Amount = model.Amount },
            cancellationToken);

        AuditNotes.Add(HttpContext, "transactionId", id.ToString());
        AuditNotes.Add(HttpContext, "amount", model.Amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
        Success("Money order added.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Audit("IdentityVerification")]
    [OnInvalidModel(nameof(Details))]
    public async Task<IActionResult> Verify(Guid id, VerifyIdentityViewModel model, CancellationToken cancellationToken)
    {
        await EnsureOwnBranchAsync(id, cancellationToken);
        var updated = await checkout.AttachVerificationAsync(
            id, model.DocumentType, model.DocumentNumber, cancellationToken);
        var result = updated.Verification!.Result;

        // Only the type and the system's result are audited. The document number is never written anywhere.
        AuditNotes.Add(HttpContext, "transactionId", id.ToString());
        AuditNotes.Add(HttpContext, "documentType", model.DocumentType.ToString());
        AuditNotes.Add(HttpContext, "result", result.ToString());
        Success($"Identity check finished: {result}.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Audit("Sale")]
    [OnInvalidModel(nameof(Details))]
    public async Task<IActionResult> Pay(Guid id, PayViewModel model, CancellationToken cancellationToken)
    {
        await EnsureOwnBranchAsync(id, cancellationToken);
        var paid = await checkout.PayAsync(id, model.Method, cancellationToken);

        AuditNotes.Add(HttpContext, "transactionId", id.ToString());
        AuditNotes.Add(HttpContext, "total", paid.Total.ToString());
        AuditNotes.Add(HttpContext, "method", model.Method.ToString());
        Success("Payment taken. The transaction is paid.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Authorize(Policy = AuthPolicies.BranchManagerOnly)]
    [Audit("Void")]
    [OnInvalidModel(nameof(Details))]
    public async Task<IActionResult> Void(Guid id, ReasonViewModel model, CancellationToken cancellationToken)
    {
        await EnsureOwnBranchAsync(id, cancellationToken);
        await reversal.VoidAsync(id, StaffId, model.Reason, cancellationToken);

        AuditNotes.Add(HttpContext, "transactionId", id.ToString());
        AuditNotes.Add(HttpContext, "reason", model.Reason);
        Success("Transaction voided.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Authorize(Policy = AuthPolicies.BranchManagerOnly)]
    [Audit("Refund")]
    [OnInvalidModel(nameof(Details))]
    public async Task<IActionResult> Refund(Guid id, ReasonViewModel model, CancellationToken cancellationToken)
    {
        await EnsureOwnBranchAsync(id, cancellationToken);
        await reversal.RefundAsync(id, StaffId, model.Reason, cancellationToken);

        AuditNotes.Add(HttpContext, "transactionId", id.ToString());
        AuditNotes.Add(HttpContext, "reason", model.Reason);
        Success("Transaction refunded.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Label(Guid id, int line, CancellationToken cancellationToken)
    {
        var transaction = await queries.GetAsync(id, cancellationToken);
        if (transaction is null)
            return NotFound();
        if (transaction.BranchCode != HomeBranch)
            return Forbid();

        var shipping = transaction.Lines.OfType<ShippingLineItem>().ToList();
        if (line < 0 || line >= shipping.Count)
            return BadRequest("That shipping line does not exist.");

        var branch = await branches.GetAsync(transaction.BranchCode, cancellationToken);

        return View(new LabelViewModel
        {
            Transaction = transaction,
            Line = shipping[line],
            FromBranch = branch is null ? transaction.BranchCode.Value : $"{branch.Name} ({branch.Code})",
            Reference = $"APX-{transaction.Id.ToString("N")[..8].ToUpperInvariant()}-{line + 1}"
        });
    }

    /// <summary>A clerk may only work on transactions of their own branch.</summary>
    private async Task EnsureOwnBranchAsync(Guid id, CancellationToken cancellationToken)
    {
        var transaction = await queries.GetAsync(id, cancellationToken)
            ?? throw new ApexPost.Application.Exceptions.NotFoundException(nameof(CounterTransaction), id);

        if (transaction.BranchCode != HomeBranch)
            throw new ApexPost.Domain.Exceptions.DomainException("This transaction belongs to another branch.");
    }
}
