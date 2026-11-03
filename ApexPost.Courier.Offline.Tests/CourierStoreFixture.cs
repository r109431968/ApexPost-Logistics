using ApexPost.Courier.Offline.Storage;

namespace ApexPost.Courier.Offline.Tests;

/// <summary>A disposable database in its own temporary folder (never the real device database).</summary>
public sealed class CourierStoreFixture : IDisposable
{
    public CourierStoreFixture()
    {
        DirectoryPath = Path.Combine(Path.GetTempPath(), "apexpost-courier-tests-" + Guid.NewGuid().ToString("N"));
        Database = new CourierDatabase(DirectoryPath);
        Repository = new CouchbaseScanRepository(Database);
    }

    public string DirectoryPath { get; }

    public CourierDatabase Database { get; }

    public CouchbaseScanRepository Repository { get; }

    public void Dispose()
    {
        Database.Dispose();
        try
        {
            Directory.Delete(DirectoryPath, recursive: true);
        }
        catch (IOException)
        {
            // a leftover temporary folder is harmless
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
