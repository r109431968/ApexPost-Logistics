using System.Diagnostics.CodeAnalysis;
using ApexPost.Application.Abstractions;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Infrastructure.Caching;

public sealed class CatalogCacheSettings
{
    public const string SectionName = "Catalog";

    public int TimeToLiveSeconds { get; set; } = 300;
}

/// <summary>
/// Keeps a full copy of a read-mostly table in memory. A cache hit finishes synchronously, so the ValueTask
/// allocates nothing. The returned entities are shared, so callers must only read them.
/// </summary>
public sealed class CachedCatalog<TEntity, TId>(
    IRepository<TEntity, TId> repository, IClock clock, TimeSpan timeToLive)
    where TEntity : Entity<TId>
    where TId : notnull
{
    private sealed record Snapshot(
        Dictionary<TId, TEntity> ById, IReadOnlyList<TEntity> All, DateTimeOffset LoadedAt);

    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private volatile Snapshot? _snapshot;
    private long _hits;
    private long _loads;

    /// <summary>Lookups answered from memory.</summary>
    public long Hits => Interlocked.Read(ref _hits);

    /// <summary>Times the repository was actually read.</summary>
    public long Loads => Interlocked.Read(ref _loads);

    public ValueTask<TEntity?> GetAsync(TId id, CancellationToken cancellationToken)
    {
        if (TryGetFresh(out var snapshot))
        {
            Interlocked.Increment(ref _hits);
            return ValueTask.FromResult(Find(snapshot, id));
        }

        return LoadAndFindAsync(id, cancellationToken);
    }

    public ValueTask<IReadOnlyList<TEntity>> ListAsync(CancellationToken cancellationToken)
    {
        if (TryGetFresh(out var snapshot))
        {
            Interlocked.Increment(ref _hits);
            return ValueTask.FromResult(snapshot.All);
        }

        return LoadAndListAsync(cancellationToken);
    }

    public void Invalidate() => _snapshot = null;

    private async ValueTask<TEntity?> LoadAndFindAsync(TId id, CancellationToken cancellationToken)
    {
        var snapshot = await EnsureLoadedAsync(cancellationToken);
        return Find(snapshot, id);
    }

    private async ValueTask<IReadOnlyList<TEntity>> LoadAndListAsync(CancellationToken cancellationToken)
    {
        var snapshot = await EnsureLoadedAsync(cancellationToken);
        return snapshot.All;
    }

    private async Task<Snapshot> EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        await _loadGate.WaitAsync(cancellationToken);
        try
        {
            // Another caller may have finished loading while this one was waiting.
            if (TryGetFresh(out var existing))
                return existing;

            Interlocked.Increment(ref _loads);

            var all = await repository.ListAsync(cancellationToken);
            var loaded = new Snapshot(all.ToDictionary(entity => entity.Id), all, clock.UtcNow);

            _snapshot = loaded;
            return loaded;
        }
        finally
        {
            _loadGate.Release();
        }
    }

    private bool TryGetFresh([NotNullWhen(true)] out Snapshot? snapshot)
    {
        snapshot = _snapshot;
        return snapshot is not null && clock.UtcNow - snapshot.LoadedAt < timeToLive;
    }

    private static TEntity? Find(Snapshot snapshot, TId id) =>
        snapshot.ById.TryGetValue(id, out var entity) ? entity : null;
}
