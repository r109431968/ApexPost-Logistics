using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Application.Abstractions;

public interface IZoneCatalog
{
    ValueTask<ShippingZone?> GetAsync(string code, CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<ShippingZone>> ListAsync(CancellationToken cancellationToken);

    void Invalidate();
}

public interface IProductCatalog
{
    ValueTask<Product?> GetAsync(Sku sku, CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<Product>> ListAsync(CancellationToken cancellationToken);

    void Invalidate();
}
