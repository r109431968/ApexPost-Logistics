using ApexPost.Application.Abstractions;
using ApexPost.Application.Specifications;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.UnitTests.Support;

/// <summary>A repository that keeps everything in memory, so service tests need no files.</summary>
public sealed class InMemoryRepository<T, TId> : IRepository<T, TId>
    where T : Entity<TId>
    where TId : notnull
{
    private readonly Dictionary<TId, T> _items = [];

    public Task<T?> GetByIdAsync(TId id, CancellationToken cancellationToken) =>
        Task.FromResult(_items.GetValueOrDefault(id));

    public Task<IReadOnlyList<T>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<T>>(_items.Values.ToList());

    public Task<IReadOnlyList<T>> ListAsync(ISpecification<T> specification, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<T>>(_items.Values.Where(specification.IsSatisfiedBy).ToList());

    public Task AddAsync(T entity, CancellationToken cancellationToken)
    {
        _items.Add(entity.Id, entity);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(T entity, CancellationToken cancellationToken)
    {
        _items[entity.Id] = entity;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(TId id, CancellationToken cancellationToken)
    {
        _items.Remove(id);
        return Task.CompletedTask;
    }
}

public sealed class FixedClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = now;
}

public sealed class RepositoryProductCatalog(IRepository<Product, Sku> repository) : IProductCatalog
{
    public async ValueTask<Product?> GetAsync(Sku sku, CancellationToken cancellationToken) =>
        await repository.GetByIdAsync(sku, cancellationToken);

    public async ValueTask<IReadOnlyList<Product>> ListAsync(CancellationToken cancellationToken) =>
        await repository.ListAsync(cancellationToken);

    public void Invalidate()
    {
    }
}

public sealed class RepositoryZoneCatalog(IRepository<ShippingZone, string> repository) : IZoneCatalog
{
    public async ValueTask<ShippingZone?> GetAsync(string code, CancellationToken cancellationToken) =>
        await repository.GetByIdAsync(code.Trim().ToUpperInvariant(), cancellationToken);

    public async ValueTask<IReadOnlyList<ShippingZone>> ListAsync(CancellationToken cancellationToken) =>
        await repository.ListAsync(cancellationToken);

    public void Invalidate()
    {
    }
}
