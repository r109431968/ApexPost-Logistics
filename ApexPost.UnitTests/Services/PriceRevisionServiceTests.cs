using ApexPost.Application.Abstractions;
using ApexPost.Application.Services;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.Exceptions;
using ApexPost.Domain.ValueObjects;
using ApexPost.UnitTests.Support;

namespace ApexPost.UnitTests.Services;

public class PriceRevisionServiceTests
{
    /// <summary>Records every price the service asks the database to write, in order.</summary>
    private sealed class RecordingWriter : IProductPriceWriter
    {
        public List<(Sku Sku, Money Price)> Calls { get; } = [];

        public Task<int> SetPriceAsync(Sku sku, Money newPrice, CancellationToken cancellationToken)
        {
            Calls.Add((sku, newPrice));
            return Task.FromResult(1);
        }
    }

    private sealed class CountingCatalog : IProductCatalog
    {
        public int Invalidations { get; private set; }

        public ValueTask<Product?> GetAsync(Sku sku, CancellationToken cancellationToken) => ValueTask.FromResult<Product?>(null);

        public ValueTask<IReadOnlyList<Product>> ListAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<Product>>([]);

        public void Invalidate() => Invalidations++;
    }

    private static async Task<(PriceRevisionService Service, RecordingWriter Writer, CountingCatalog Catalog)> CreateAsync()
    {
        var products = new InMemoryRepository<Product, Sku>();
        await products.AddAsync(new Product(new Sku("STA-0002"), "Notebook", ProductCategory.Stationery, new Money(4.00m), TaxCategory.Standard), default);
        await products.AddAsync(new Product(new Sku("STA-0001"), "Gel pen", ProductCategory.Stationery, new Money(2.50m), TaxCategory.Standard), default);
        await products.AddAsync(new Product(new Sku("PKG-0001"), "Small box", ProductCategory.Packaging, new Money(1.20m), TaxCategory.Reduced), default);

        var writer = new RecordingWriter();
        var catalog = new CountingCatalog();
        return (new PriceRevisionService(products, writer, catalog, new SpyUnitOfWork()), writer, catalog);
    }

    [Fact]
    public async Task Only_the_chosen_category_is_changed_and_prices_are_rounded_to_two_decimals()
    {
        var (service, writer, _) = await CreateAsync();

        var changed = await service.ReviseCategoryAsync(ProductCategory.Stationery, 10m, default);

        Assert.Equal(2, changed);
        Assert.All(writer.Calls, call => Assert.StartsWith("STA-", call.Sku.Value));
        Assert.Equal(2.75m, writer.Calls.Single(c => c.Sku.Value == "STA-0001").Price.Amount);
        Assert.Equal(4.40m, writer.Calls.Single(c => c.Sku.Value == "STA-0002").Price.Amount);
    }

    [Fact]
    public async Task Products_are_updated_in_SKU_order_so_two_revisions_cannot_deadlock()
    {
        var (service, writer, _) = await CreateAsync();

        await service.ReviseCategoryAsync(ProductCategory.Stationery, -10m, default);

        Assert.Equal(new[] { "STA-0001", "STA-0002" }, writer.Calls.Select(c => c.Sku.Value).ToArray());
    }

    [Fact]
    public async Task The_product_cache_is_cleared_after_a_revision()
    {
        var (service, _, catalog) = await CreateAsync();

        await service.ReviseCategoryAsync(ProductCategory.Packaging, 5m, default);

        Assert.Equal(1, catalog.Invalidations);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100.5)]
    [InlineData(-101)]
    public async Task A_zero_or_out_of_range_percentage_is_rejected_and_nothing_is_written(double percent)
    {
        var (service, writer, catalog) = await CreateAsync();

        await Assert.ThrowsAsync<DomainException>(() =>
            service.ReviseCategoryAsync(ProductCategory.Stationery, (decimal)percent, default));

        Assert.Empty(writer.Calls);
        Assert.Equal(0, catalog.Invalidations);
    }
}
