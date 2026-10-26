using ApexPost.Application.Abstractions;
using ApexPost.Infrastructure.Persistence.Json;

namespace ApexPost.Infrastructure.Security;

public sealed record CredentialRecord
{
    public required Guid StaffId { get; init; }
    public required string Hash { get; init; }
}

/// <summary>Stores only password hashes (never passwords), one record per staff member.</summary>
public sealed class JsonCredentialStore(string filePath) : ICredentialStore
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Dictionary<Guid, string>? _hashes;

    public async Task<string?> GetHashAsync(Guid staffId, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var current = await LoadAsync(cancellationToken);
            return current.TryGetValue(staffId, out var hash) ? hash : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SetHashAsync(Guid staffId, string hash, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var current = await LoadAsync(cancellationToken);
            var updated = new Dictionary<Guid, string>(current) { [staffId] = hash };

            var records = updated
                .Select(pair => new CredentialRecord { StaffId = pair.Key, Hash = pair.Value })
                .OrderBy(record => record.StaffId)
                .ToList();

            await JsonFile.WriteListAsync(filePath, records, cancellationToken);
            _hashes = updated;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<Dictionary<Guid, string>> LoadAsync(CancellationToken cancellationToken)
    {
        if (_hashes is not null)
            return _hashes;

        var list = await JsonFile.ReadListAsync<CredentialRecord>(filePath, cancellationToken);
        _hashes = list.ToDictionary(record => record.StaffId, record => record.Hash);
        return _hashes;
    }
}
