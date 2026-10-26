using ApexPost.Application.Abstractions;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Infrastructure.Caching;

public sealed class ZoneCatalog : IZoneCatalog
{
    private readonly CachedCatalog<ShippingZone, string> _cache;

    public ZoneCatalog(IRepository<ShippingZone, string> repository, IClock clock, CatalogCacheSettings settings) =>
        _cache = new CachedCatalog<ShippingZone, string>(
            repository, clock, TimeSpan.FromSeconds(settings.TimeToLiveSeconds));

    public long CacheHits => _cache.Hits;

    public long CacheLoads => _cache.Loads;

    public ValueTask<ShippingZone?> GetAsync(string code, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        return _cache.GetAsync(code.Trim().ToUpperInvariant(), cancellationToken);
    }

    public ValueTask<IReadOnlyList<ShippingZone>> ListAsync(CancellationToken cancellationToken) =>
        _cache.ListAsync(cancellationToken);

    public void Invalidate() => _cache.Invalidate();
}

public sealed class ProductCatalog : IProductCatalog
{
    private readonly CachedCatalog<Product, Sku> _cache;

    public ProductCatalog(IRepository<Product, Sku> repository, IClock clock, CatalogCacheSettings settings) =>
        _cache = new CachedCatalog<Product, Sku>(
            repository, clock, TimeSpan.FromSeconds(settings.TimeToLiveSeconds));

    public long CacheHits => _cache.Hits;

    public long CacheLoads => _cache.Loads;

    public ValueTask<Product?> GetAsync(Sku sku, CancellationToken cancellationToken) =>
        _cache.GetAsync(sku, cancellationToken);

    public ValueTask<IReadOnlyList<Product>> ListAsync(CancellationToken cancellationToken) =>
        _cache.ListAsync(cancellationToken);

    public void Invalidate() => _cache.Invalidate();
}
