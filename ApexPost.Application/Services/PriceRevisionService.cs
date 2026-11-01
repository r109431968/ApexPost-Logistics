using ApexPost.Application.Abstractions;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.Exceptions;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Application.Services;

/// <summary>
/// Category-wide price revision. The new price of each product is calculated here in the domain (Money rounds to 2 decimals),
/// then written with a set-based UPDATE per product. All the updates run in ONE database transaction: either every
/// price in the category changes or none does. The product cache is cleared afterwards so the next request sees the new prices.
/// Old transaction lines keep the price they were sold at (the line stores its own UnitPrice).
/// </summary>
public sealed class PriceRevisionService(
    IRepository<Product, Sku> products,
    IProductPriceWriter writer,
    IProductCatalog catalog,
    IUnitOfWork unitOfWork) : IPriceRevisionService
{
    public const decimal MaxPercent = 100m;

    public async Task<int> ReviseCategoryAsync(
        ProductCategory category, decimal percent, CancellationToken cancellationToken)
    {
        if (percent == 0m)
            throw new DomainException("The percentage must not be zero.");
        if (percent is < -MaxPercent or > MaxPercent)
            throw new DomainException($"The percentage must be between -{MaxPercent} and {MaxPercent}.");

        var all = await products.ListAsync(cancellationToken);
        var inCategory = all.Where(product => product.Category == category).OrderBy(product => product.Sku.Value).ToList();
        var factor = 1m + percent / 100m;
        var changed = 0;

        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            changed = 0;
            foreach (var product in inCategory)
            {
                var newPrice = new Money(product.UnitPrice.Amount * factor, product.UnitPrice.Currency);
                changed += await writer.SetPriceAsync(product.Sku, newPrice, token);
            }
        }, cancellationToken);

        catalog.Invalidate();
        return changed;
    }
}
