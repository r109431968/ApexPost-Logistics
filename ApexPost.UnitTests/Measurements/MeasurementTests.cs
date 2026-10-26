using System.Diagnostics;
using System.Text;
using ApexPost.Application.Abstractions;
using ApexPost.Application.Reports;
using ApexPost.Application.Settings;
using ApexPost.Application.Specifications;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;
using ApexPost.Infrastructure.Caching;
using ApexPost.Infrastructure.Shipping;
using ApexPost.UnitTests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApexPost.UnitTests.Measurements;

/// <summary>Wraps a repository and adds a fixed delay to every read, to imitate a slow disk or database.</summary>
internal sealed class SlowRepository<T, TId>(IRepository<T, TId> inner, int delayMilliseconds) : IRepository<T, TId>
    where T : Entity<TId>
    where TId : notnull
{
    public async Task<T?> GetByIdAsync(TId id, CancellationToken cancellationToken)
    {
        await Task.Delay(delayMilliseconds, cancellationToken);
        return await inner.GetByIdAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<T>> ListAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(delayMilliseconds, cancellationToken);
        return await inner.ListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<T>> ListAsync(ISpecification<T> specification, CancellationToken cancellationToken)
    {
        await Task.Delay(delayMilliseconds, cancellationToken);
        return await inner.ListAsync(specification, cancellationToken);
    }

    public Task AddAsync(T entity, CancellationToken cancellationToken) => inner.AddAsync(entity, cancellationToken);

    public Task UpdateAsync(T entity, CancellationToken cancellationToken) => inner.UpdateAsync(entity, cancellationToken);

    public Task DeleteAsync(TId id, CancellationToken cancellationToken) => inner.DeleteAsync(id, cancellationToken);
}

/// <summary>
/// Not a pass/fail test. It measures three things for DESIGN.md section 8 and writes them to a text file:
/// the catalogue cache, Task.WhenAll against awaiting one after another, and the SemaphoreSlim limit on courier lookups.
/// </summary>
public class MeasurementTests
{
    private const int ReadDelayMilliseconds = 50;

    [Fact]
    [Trait("Category", "Measurement")]
    public async Task Collect_the_numbers_for_the_design_document()
    {
        var report = new StringBuilder();
        report.AppendLine("ApexPost measurements (numbers differ a little on every run and every machine)");
        report.AppendLine(FormattableString.Invariant($"Taken: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}"));
        report.AppendLine();

        await MeasureCacheAsync(report);
        await MeasureWhenAllAsync(report);
        await MeasureSemaphoreAsync(report);

        var path = Path.Combine(Path.GetTempPath(), "apexpost-measurements.txt");
        await File.WriteAllTextAsync(path, report.ToString());

        Assert.True(File.Exists(path));
    }

    private static async Task MeasureCacheAsync(StringBuilder report)
    {
        var products = new InMemoryRepository<Product, Sku>();
        for (var i = 1; i <= 32; i++)
        {
            await products.AddAsync(
                new Product(new Sku($"STA-{i:0000}"), $"Product {i}", ProductCategory.Stationery, new Money(2m), TaxCategory.Standard),
                CancellationToken.None);
        }

        var slow = new SlowRepository<Product, Sku>(products, ReadDelayMilliseconds);
        var catalogue = new ProductCatalog(slow, new FixedClock(DateTimeOffset.UtcNow), new CatalogCacheSettings());
        var sku = new Sku("STA-0001");

        var watch = Stopwatch.StartNew();
        await catalogue.GetAsync(sku, CancellationToken.None);
        var cold = watch.Elapsed.TotalMilliseconds;

        const int lookups = 1000;
        watch.Restart();
        for (var i = 0; i < lookups; i++)
            await catalogue.GetAsync(sku, CancellationToken.None);
        var warmTotal = watch.Elapsed.TotalMilliseconds;

        report.AppendLine("1. Catalogue cache (ValueTask)");
        report.AppendLine(FormattableString.Invariant($"   Table read costs {ReadDelayMilliseconds} ms (simulated slow storage)."));
        report.AppendLine(FormattableString.Invariant($"   Loads: {catalogue.CacheLoads}   Hits: {catalogue.CacheHits}   (1 cold lookup + {lookups} warm lookups)"));
        report.AppendLine(FormattableString.Invariant($"   Cold lookup:  {cold:F2} ms"));
        report.AppendLine(FormattableString.Invariant($"   Warm lookup:  {warmTotal / lookups:F5} ms each ({warmTotal:F2} ms for {lookups})"));
        report.AppendLine();
    }

    private static async Task MeasureWhenAllAsync(StringBuilder report)
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 10, 26, 10, 0, 0, TimeSpan.Zero));
        var transactions = new SlowRepository<CounterTransaction, Guid>(
            new InMemoryRepository<CounterTransaction, Guid>(), ReadDelayMilliseconds);
        var stock = new SlowRepository<StockItem, string>(
            new InMemoryRepository<StockItem, string>(), ReadDelayMilliseconds);
        var products = new SlowRepository<Product, Sku>(
            new InMemoryRepository<Product, Sku>(), ReadDelayMilliseconds);

        var reports = new ReportService(transactions, stock, products, new StockSettings(), new MoneyOrderSettings(), clock);
        var branch = new BranchCode("BR001");
        var day = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);

        var watch = Stopwatch.StartNew();
        await reports.DashboardAsync(branch, CancellationToken.None);
        var dashboard = watch.Elapsed.TotalMilliseconds;

        watch.Restart();
        await reports.DailySalesAsync(branch, day, CancellationToken.None);
        await reports.LowStockAsync(branch, null, CancellationToken.None);
        await reports.ShippingVolumeAsync(day, day, branch, CancellationToken.None);
        var oneAfterAnother = watch.Elapsed.TotalMilliseconds;

        watch.Restart();
        var sales = reports.DailySalesAsync(branch, day, CancellationToken.None);
        var low = reports.LowStockAsync(branch, null, CancellationToken.None);
        var shipping = reports.ShippingVolumeAsync(day, day, branch, CancellationToken.None);
        await Task.WhenAll(sales, low, shipping);
        var together = watch.Elapsed.TotalMilliseconds;

        report.AppendLine("2. Task.WhenAll");
        report.AppendLine(FormattableString.Invariant($"   Every repository read costs {ReadDelayMilliseconds} ms (simulated slow storage)."));
        report.AppendLine(FormattableString.Invariant($"   DashboardAsync (uses WhenAll for its three queries): {dashboard:F0} ms"));
        report.AppendLine(FormattableString.Invariant($"   Same kind of three queries awaited one after another: {oneAfterAnother:F0} ms"));
        report.AppendLine(FormattableString.Invariant($"   The same three queries started together with Task.WhenAll: {together:F0} ms"));
        report.AppendLine("   (The dashboard's alerts query is private, so sales, low stock and shipping volume stand in for the comparison.)");
        report.AppendLine();
    }

    private static async Task<double> RunCourierAsync(int slots)
    {
        using var courier = new SimulatedCourierTrackingService(
            new CourierSettings
            {
                TimeoutMilliseconds = 20000,
                SimulatedLatencyMilliseconds = 150,
                MaxConcurrentLookups = slots
            },
            new FixedClock(DateTimeOffset.UtcNow),
            NullLogger<SimulatedCourierTrackingService>.Instance);

        var watch = Stopwatch.StartNew();
        var lookups = Enumerable.Range(1, 10)
            .Select(i => courier.GetStatusAsync($"APX{i:000000}", CancellationToken.None))
            .ToList();
        await Task.WhenAll(lookups);
        return watch.Elapsed.TotalMilliseconds;
    }

    private static async Task MeasureSemaphoreAsync(StringBuilder report)
    {
        var one = await RunCourierAsync(1);
        var three = await RunCourierAsync(3);
        var ten = await RunCourierAsync(10);

        report.AppendLine("3. SemaphoreSlim (courier lookups, simulated 150 ms each, 10 parallel lookups)");
        report.AppendLine(FormattableString.Invariant($"   Limit 1  slot : {one:F0} ms  (about 10 waves)"));
        report.AppendLine(FormattableString.Invariant($"   Limit 3  slots: {three:F0} ms  (about 4 waves, the configured limit)"));
        report.AppendLine(FormattableString.Invariant($"   Limit 10 slots: {ten:F0} ms  (about 1 wave)"));
    }
}