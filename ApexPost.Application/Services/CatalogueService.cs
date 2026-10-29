using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Application.Exceptions;
using ApexPost.Application.Settings;
using ApexPost.Application.Specifications;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.Events;
using ApexPost.Domain.Exceptions;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Application.Services;

public sealed class CatalogueService(
    IProductCatalog products,
    IRepository<StockItem, string> stock,
    IRepository<Staff, Guid> staff,
    StockSettings stockSettings,
    CounterEvents events,
    IClock clock) : ICatalogueService
{
    public async Task<IReadOnlyList<CatalogueRow>> ListAsync(
        BranchCode branch, ProductCategory? category, string? search, CancellationToken cancellationToken)
    {
        var all = await products.ListAsync(cancellationToken);
        var branchStock = await stock.ListAsync(StockSpecs.ForBranch(branch), cancellationToken);
        var quantities = branchStock.ToDictionary(item => item.Sku, item => item.Quantity);
        var term = search?.Trim();

        return all
            .Where(product => category is null || product.Category == category)
            .Where(product => string.IsNullOrEmpty(term)
                              || product.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                              || product.Sku.Value.Contains(term, StringComparison.OrdinalIgnoreCase))
            .OrderBy(product => product.Sku.Value, StringComparer.Ordinal)
            .Select(product =>
            {
                var quantity = quantities.TryGetValue(product.Sku, out var found) ? found : 0;
                return new CatalogueRow(
                    product.Sku, product.Name, product.Category, product.UnitPrice, product.TaxCategory,
                    quantity, quantity <= stockSettings.LowStockThreshold);
            })
            .ToList();
    }

    public async Task<StockItem> AdjustStockAsync(
        Guid actorId, Sku sku, int delta, string reason, CancellationToken cancellationToken)
    {
        if (delta == 0)
            throw new DomainException("The adjustment cannot be zero.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("A reason is required for a stock adjustment.");

        var actor = await staff.GetByIdAsync(actorId, cancellationToken)
            ?? throw new NotFoundException(nameof(Staff), actorId);
        if (!actor.IsActive || actor.Role != StaffRole.BranchManager)
            throw new DomainException("Only an active Branch Manager can adjust stock.");

        if (await products.GetAsync(sku, cancellationToken) is null)
            throw new NotFoundException(nameof(Product), sku);

        var gate = StockGate.For(actor.HomeBranch);
        await gate.WaitAsync(cancellationToken);
        try
        {
            var item = await stock.GetByIdAsync(StockItem.KeyFor(actor.HomeBranch, sku), cancellationToken);

            if (item is null)
            {
                if (delta < 0)
                    throw new InsufficientStockException(sku, 0, -delta);

                item = new StockItem(actor.HomeBranch, sku, delta);
                await stock.AddAsync(item, cancellationToken);
            }
            else
            {
                if (delta > 0)
                    item.Add(delta);
                else
                    item.Remove(-delta);

                await stock.UpdateAsync(item, cancellationToken);
            }

            if (item.Quantity <= stockSettings.LowStockThreshold)
                events.Publish(new StockLowEvent(
                    item.BranchCode, item.Sku, item.Quantity, stockSettings.LowStockThreshold, clock.UtcNow));

            return item;
        }
        finally
        {
            gate.Release();
        }
    }
}
