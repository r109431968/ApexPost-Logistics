using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Application.Abstractions;

/// <summary>
/// Port for a set-based price change: the new price is written straight to the database with one UPDATE statement
/// (EF Core ExecuteUpdateAsync) without loading and tracking the product.
/// </summary>
public interface IProductPriceWriter
{
    /// <returns>The number of products that were changed (0 when the SKU does not exist).</returns>
    Task<int> SetPriceAsync(Sku sku, Money newPrice, CancellationToken cancellationToken);
}

/// <summary>Raises or lowers the prices of a whole category by a percentage (an administrator task).</summary>
public interface IPriceRevisionService
{
    /// <returns>The number of products whose price changed.</returns>
    Task<int> ReviseCategoryAsync(ProductCategory category, decimal percent, CancellationToken cancellationToken);
}
