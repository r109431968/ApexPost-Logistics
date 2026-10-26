using System.Text.Json;
using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Infrastructure.Persistence.Json;

namespace ApexPost.Infrastructure.Auditing;

/// <summary>
/// Append-only audit log: one JSON object per line. The class has no way to change or remove
/// an entry once it is written.
/// </summary>
public sealed class JsonLinesAuditStore : IAuditStore
{
    private static readonly JsonSerializerOptions LineOptions = new(JsonDefaultsAccessor.Options)
    {
        WriteIndented = false
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _filePath;

    public JsonLinesAuditStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }

    public async Task AppendAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var line = JsonSerializer.Serialize(entry, LineOptions) + Environment.NewLine;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            await File.AppendAllTextAsync(_filePath, line, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Returns up to <paramref name="count"/> entries, newest first.</summary>
    public async Task<IReadOnlyList<AuditEntry>> ReadRecentAsync(int count, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        string[] lines;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_filePath))
                return [];

            lines = await File.ReadAllLinesAsync(_filePath, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }

        var result = new List<AuditEntry>(Math.Min(count, lines.Length));

        for (var index = lines.Length - 1; index >= 0 && result.Count < count; index--)
        {
            if (string.IsNullOrWhiteSpace(lines[index]))
                continue;

            try
            {
                var entry = JsonSerializer.Deserialize<AuditEntry>(lines[index], LineOptions);
                if (entry is null)
                    throw new InvalidDataException($"Audit file line {index + 1} is empty.");

                result.Add(entry);
            }
            catch (JsonException ex)
            {
                // A damaged audit line is reported, never skipped silently.
                throw new InvalidDataException($"Audit file line {index + 1} is damaged.", ex);
            }
        }

        return result;
    }
}
