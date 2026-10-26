using ApexPost.Application.Abstractions;
using ApexPost.Application.Exceptions;
using ApexPost.Application.Specifications;
using ApexPost.Domain.Entities;
using ApexPost.Infrastructure.Persistence.Json.Mapping;

namespace ApexPost.Infrastructure.Persistence.Json;

/// <summary>
/// One generic repository for every table. Records are kept in memory and each read builds a fresh entity,
/// so a failed operation never leaves a half-changed object in the cache. Every write goes to a temp file first.
/// </summary>
public sealed class JsonRepository<TEntity, TId, TRecord> : IRepository<TEntity, TId>
    where TEntity : Entity<TId>
    where TId : notnull
    where TRecord : class
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _filePath;
    private readonly IRecordMapper<TEntity, TRecord> _mapper;
    private Dictionary<string, TRecord>? _records;

    public JsonRepository(string filePath, IRecordMapper<TEntity, TRecord> mapper)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(mapper);

        _filePath = filePath;
        _mapper = mapper;
    }

    public async Task<TEntity?> GetByIdAsync(TId id, CancellationToken cancellationToken)
    {
        var key = ToKey(id);
        TRecord? record = null;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var current = await CurrentAsync(cancellationToken);
            current.TryGetValue(key, out record);
        }
        finally
        {
            _gate.Release();
        }

        return record is null ? null : _mapper.ToEntity(record);
    }

    public async Task<IReadOnlyList<TEntity>> ListAsync(CancellationToken cancellationToken)
    {
        var snapshot = await SnapshotAsync(cancellationToken);
        return snapshot.Select(_mapper.ToEntity).ToList();
    }

    public async Task<IReadOnlyList<TEntity>> ListAsync(
        ISpecification<TEntity> specification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(specification);

        var all = await ListAsync(cancellationToken);
        return all.Where(specification.IsSatisfiedBy).ToList();
    }

    public async Task AddAsync(TEntity entity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var key = ToKey(entity.Id);
        var record = RecordFor(entity, key);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var current = await CurrentAsync(cancellationToken);
            if (current.ContainsKey(key))
                throw new InvalidOperationException($"{typeof(TEntity).Name} '{key}' already exists.");

            var updated = new Dictionary<string, TRecord>(current, StringComparer.Ordinal) { [key] = record };
            await PersistAsync(updated, cancellationToken);
            _records = updated;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task UpdateAsync(TEntity entity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var key = ToKey(entity.Id);
        var record = RecordFor(entity, key);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var current = await CurrentAsync(cancellationToken);
            if (!current.ContainsKey(key))
                throw new NotFoundException(typeof(TEntity).Name, key);

            var updated = new Dictionary<string, TRecord>(current, StringComparer.Ordinal) { [key] = record };
            await PersistAsync(updated, cancellationToken);
            _records = updated;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteAsync(TId id, CancellationToken cancellationToken)
    {
        var key = ToKey(id);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var current = await CurrentAsync(cancellationToken);
            if (!current.ContainsKey(key))
                throw new NotFoundException(typeof(TEntity).Name, key);

            var updated = new Dictionary<string, TRecord>(current, StringComparer.Ordinal);
            updated.Remove(key);
            await PersistAsync(updated, cancellationToken);
            _records = updated;
        }
        finally
        {
            _gate.Release();
        }
    }

    // Must be called while holding the gate.
    private async Task<Dictionary<string, TRecord>> CurrentAsync(CancellationToken cancellationToken)
    {
        if (_records is not null)
            return _records;

        var list = await JsonFile.ReadListAsync<TRecord>(_filePath, cancellationToken);
        var loaded = new Dictionary<string, TRecord>(StringComparer.Ordinal);

        foreach (var record in list)
        {
            var key = _mapper.KeyOf(record);
            if (!loaded.TryAdd(key, record))
                throw new InvalidDataException(
                    $"The data file '{Path.GetFileName(_filePath)}' contains the key '{key}' twice.");
        }

        _records = loaded;
        return loaded;
    }

    private async Task<List<TRecord>> SnapshotAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var current = await CurrentAsync(cancellationToken);
            return current.Values.ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    private Task PersistAsync(Dictionary<string, TRecord> records, CancellationToken cancellationToken)
    {
        var ordered = records
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => pair.Value)
            .ToList();

        return JsonFile.WriteListAsync(_filePath, ordered, cancellationToken);
    }

    private TRecord RecordFor(TEntity entity, string key)
    {
        var record = _mapper.ToRecord(entity);

        // A mapper whose key differs from the entity key would silently lose or duplicate data.
        if (_mapper.KeyOf(record) != key)
            throw new InvalidOperationException(
                $"The {typeof(TEntity).Name} mapper produced a different key than the entity ('{key}').");

        return record;
    }

    private static string ToKey(TId id) =>
        id.ToString() ?? throw new ArgumentException("The id has no text form.", nameof(id));
}
