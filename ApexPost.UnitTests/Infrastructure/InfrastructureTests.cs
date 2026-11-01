using ApexPost.Application.DTOs;
using ApexPost.Application.Exceptions;
using ApexPost.Application.Services;
using ApexPost.Application.Settings;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;
using ApexPost.Infrastructure.Caching;
using ApexPost.Infrastructure.Shipping;
using ApexPost.UnitTests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApexPost.UnitTests.Infrastructure;

public class AuditStoreTests
{
    [Fact]
    public void The_audit_entry_has_no_field_that_could_hold_a_secret()
    {
        var names = typeof(AuditEntry).GetProperties().Select(p => p.Name.ToLowerInvariant()).ToList();

        Assert.DoesNotContain(names, n => n.Contains("password") || n.Contains("apikey") || n.Contains("card") || n.Contains("documentnumber"));
    }

    [Fact]
    public void The_audit_store_interface_can_only_append_and_read()
    {
        var methods = typeof(ApexPost.Application.Abstractions.IAuditStore).GetMethods().Select(m => m.Name).OrderBy(n => n);

        Assert.Equal(new[] { "AppendAsync", "ReadRecentAsync" }, methods.ToArray());
    }
}

public class CacheAndCourierTests
{
    [Fact]
    public async Task The_catalogue_cache_loads_once_and_answers_later_lookups_from_memory()
    {
        var repository = new InMemoryRepository<Product, Sku>();
        await repository.AddAsync(
            new Product(new Sku("STA-0001"), "Pen", ProductCategory.Stationery, new Money(2m), TaxCategory.Standard), default);
        var catalogue = new ProductCatalog(repository, new FixedClock(DateTimeOffset.UtcNow), new CatalogCacheSettings());

        for (var i = 0; i < 100; i++)
            Assert.NotNull(await catalogue.GetAsync(new Sku("STA-0001"), default));

        Assert.Equal(1, catalogue.CacheLoads);
        Assert.Equal(99, catalogue.CacheHits);
    }

    [Fact]
    public async Task Invalidate_forces_a_reload()
    {
        var repository = new InMemoryRepository<Product, Sku>();
        var catalogue = new ProductCatalog(repository, new FixedClock(DateTimeOffset.UtcNow), new CatalogCacheSettings());

        await catalogue.ListAsync(default);
        catalogue.Invalidate();
        await catalogue.ListAsync(default);

        Assert.Equal(2, catalogue.CacheLoads);
    }

    private static SimulatedCourierTrackingService Courier(int timeoutMs = 300, int latencyMs = 0, int slots = 3) =>
        new(new CourierSettings
        {
            TimeoutMilliseconds = timeoutMs,
            SimulatedLatencyMilliseconds = latencyMs,
            MaxConcurrentLookups = slots
        }, new FixedClock(DateTimeOffset.UtcNow), NullLogger<SimulatedCourierTrackingService>.Instance);

    [Fact]
    public async Task A_normal_lookup_returns_a_status()
    {
        using var courier = Courier();

        var result = await courier.GetStatusAsync("APX123456", default);

        Assert.Equal("APX123456", result.TrackingNumber);
        Assert.False(string.IsNullOrWhiteSpace(result.Status));
    }

    [Fact]
    public async Task A_slow_courier_becomes_a_timeout_failure()
    {
        using var courier = Courier(timeoutMs: 100);

        await Assert.ThrowsAsync<ExternalServiceTimeoutException>(() => courier.GetStatusAsync("SLOW0001", default));
    }

    [Fact]
    public async Task A_broken_courier_becomes_an_external_service_failure_not_a_timeout()
    {
        using var courier = Courier();

        var error = await Assert.ThrowsAsync<ExternalServiceException>(() => courier.GetStatusAsync("FAIL0001", default));

        Assert.IsNotType<ExternalServiceTimeoutException>(error);
    }

    [Fact]
    public async Task Cancellation_by_the_caller_is_not_reported_as_a_timeout_or_failure()
    {
        using var courier = Courier(timeoutMs: 5000);
        using var source = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        var error = await Record.ExceptionAsync(() => courier.GetStatusAsync("SLOW0001", source.Token));

        Assert.IsAssignableFrom<OperationCanceledException>(error);
        Assert.IsNotType<ExternalServiceTimeoutException>(error);
    }
}

public class PasswordHasherTests
{
    private static readonly Pbkdf2PasswordHasher Hasher = new(new PasswordSettings { Iterations = 1000 });

    [Fact]
    public void A_password_verifies_against_its_own_hash_only()
    {
        var hash = Hasher.Hash("correct horse battery staple");

        Assert.True(Hasher.Verify("correct horse battery staple", hash));
        Assert.False(Hasher.Verify("wrong password", hash));
    }

    [Fact]
    public void Two_hashes_of_the_same_password_differ_because_of_the_random_salt()
    {
        Assert.NotEqual(Hasher.Hash("same password 1"), Hasher.Hash("same password 1"));
    }

    [Fact]
    public void A_damaged_hash_is_rejected_instead_of_throwing()
    {
        Assert.False(Hasher.Verify("anything", "not-a-hash"));
        Assert.False(Hasher.Verify("anything", "pbkdf2-sha256$abc$@@@$@@@"));
    }
}
