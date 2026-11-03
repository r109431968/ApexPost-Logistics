using ApexPost.Courier.Offline.Abstractions;

namespace ApexPost.Courier.Offline.Storage;

/// <summary>
/// Listens to the collection change notifications of a <see cref="CourierDatabase"/>.
/// For every change it writes one log line with the ids of the changed documents and the live number of pending scans.
/// </summary>
public sealed class PendingScanMonitor : IDisposable
{
    private readonly CourierDatabase _database;
    private readonly IScanRepository _repository;
    private readonly TextWriter _output;
    private readonly List<string> _lines = [];
    private readonly Lock _gate = new();
    private bool _disposed;

    public PendingScanMonitor(CourierDatabase database, IScanRepository repository, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(output);

        _database = database;
        _repository = repository;
        _output = output;
        _database.ScansChanged += OnScansChanged;
    }

    /// <summary>The number of scans that are Pending right now.</summary>
    public int PendingCount => _repository.CountPending();

    /// <summary>A copy of the log lines written so far.</summary>
    public IReadOnlyList<string> Lines
    {
        get
        {
            lock (_gate)
            {
                return [.. _lines];
            }
        }
    }

    private void OnScansChanged(object? sender, ScansChangedEventArgs e)
    {
        string line;
        try
        {
            var pending = _repository.CountPending();
            line = $"[listener] changed: {string.Join(", ", e.DocumentIds)} | pending scans: {pending}";
        }
        catch (Exception ex)
        {
            // This runs on a background thread. An exception here must not end the process.
            line = $"[listener] changed: {string.Join(", ", e.DocumentIds)} | pending count unavailable ({ex.GetType().Name})";
        }

        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _lines.Add(line);
            _output.WriteLine(line);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        _database.ScansChanged -= OnScansChanged;
    }
}
