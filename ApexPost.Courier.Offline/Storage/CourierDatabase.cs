using Couchbase.Lite;
using Couchbase.Lite.Query;

namespace ApexPost.Courier.Offline.Storage;

/// <summary>
/// Owns the embedded Couchbase Lite database "apexpost_courier" and its default collection.
/// It opens the database, makes sure the value indexes exist, registers one collection change listener,
/// and on Dispose removes the listener token before it closes the database.
/// One process must own the database files (see OFFLINE-DESIGN.md).
/// </summary>
public sealed class CourierDatabase : IDisposable
{
    public const string DefaultDatabaseName = "apexpost_courier";
    public const string TrackingNumberIndexName = "idx_scan_trackingNumber";
    public const string SyncStatusIndexName = "idx_scan_syncStatus";

    private readonly Database _database;
    private ListenerToken _listenerToken;
    private bool _disposed;

    /// <param name="directory">Folder for the database files. When null, the Couchbase Lite default folder is used.</param>
    /// <param name="databaseName">Name of the database.</param>
    public CourierDatabase(string? directory = null, string databaseName = DefaultDatabaseName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

        var configuration = new DatabaseConfiguration();
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
            configuration = new DatabaseConfiguration { Directory = directory };
        }

        _database = new Database(databaseName, configuration);
        Collection = _database.GetDefaultCollection();

        EnsureIndexes();
        _listenerToken = Collection.AddChangeListener(OnCollectionChanged);
    }

    /// <summary>Raised (on a background thread) after documents in the collection changed.</summary>
    public event EventHandler<ScansChangedEventArgs>? ScansChanged;

    public string Name => _database.Name;

    /// <summary>Folder of the database files.</summary>
    public string? Path => _database.Path;

    internal Collection Collection { get; }

    /// <summary>Names of the indexes that exist on the collection.</summary>
    public IReadOnlyList<string> IndexNames() => Collection.GetIndexes().ToList();

    private void EnsureIndexes()
    {
        // CreateIndex does nothing when an identical index already exists, so it is safe on every start.
        Collection.CreateIndex(TrackingNumberIndexName, new ValueIndexConfiguration("trackingNumber"));
        Collection.CreateIndex(SyncStatusIndexName, new ValueIndexConfiguration("syncStatus"));
    }

    private void OnCollectionChanged(object? sender, CollectionChangedEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        ScansChanged?.Invoke(this, new ScansChangedEventArgs(e.DocumentIDs));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ScansChanged = null;
        _listenerToken.Remove();   // stop the notifications first, then close the database
        _database.Dispose();
    }
}
