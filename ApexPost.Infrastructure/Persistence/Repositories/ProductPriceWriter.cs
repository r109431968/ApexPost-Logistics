using ApexPost.Application.Abstractions;
using ApexPost.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Persistence.Sql.Repositories;

/// <summary>
/// Set-based price update. ExecuteUpdateAsync sends one UPDATE ... WHERE Sku = @p statement and bypasses change tracking.
/// The price is a value converter (Money) column, so the new Money value is calculated by the caller and written as is.
/// </summary>
public sealed class ProductPriceWriter(ApexPostDbContext db) : IProductPriceWriter
{
    public Task<int> SetPriceAsync(Sku sku, Money newPrice, CancellationToken cancellationToken) =>
        db.Products
            .Where(product => product.Sku == sku)
            .ExecuteUpdateAsync(setters => setters.SetProperty(product => product.UnitPrice, newPrice), cancellationToken);
}
