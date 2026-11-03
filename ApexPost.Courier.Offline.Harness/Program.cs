using ApexPost.Courier.Offline.Domain;
using ApexPost.Courier.Offline.Storage;
using ApexPost.Courier.Offline.Sync;

// Console harness for the courier offline store (Part A). It works with no network at all.
// All sample data is fictional.

var dataDirectory = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ApexPost", "courier-offline");

// using declarations are disposed in reverse order: the monitor stops listening first, then the database removes its listener token and closes.
using var database = new CourierDatabase(dataDirectory);
var repository = new CouchbaseScanRepository(database);
using var monitor = new PendingScanMonitor(database, repository, Console.Out);
var outbox = new OutboxService(repository, new FakeShipmentUploader(TimeSpan.FromMilliseconds(150)));

Console.WriteLine($"Database '{database.Name}' opened at {database.Path}");
Console.WriteLine($"Indexes: {string.Join(", ", database.IndexNames())}");

while (true)
{
    Console.WriteLine();
    Console.WriteLine($"=== ApexPost courier (offline) | pending scans: {monitor.PendingCount} ===");
    Console.WriteLine("1. Add Scan");
    Console.WriteLine("2. Find by Tracking Number");
    Console.WriteLine("3. List Pending");
    Console.WriteLine("4. Simulate Sync");
    Console.WriteLine("5. Delete Scan");
    Console.WriteLine("6. Exit");
    Console.WriteLine("7. (extra) Count Scans per Type for a Courier");
    Console.Write("Choose: ");

    var choice = Console.ReadLine()?.Trim();
    if (choice is null || choice == "6")
    {
        break;
    }

    try
    {
        switch (choice)
        {
            case "1":
                AddScan();
                break;
            case "2":
                FindByTracking();
                break;
            case "3":
                ListPending();
                break;
            case "4":
                await SimulateSyncAsync();
                break;
            case "5":
                DeleteScan();
                break;
            case "7":
                CountPerType();
                break;
            default:
                Console.WriteLine("Please choose 1 to 7.");
                break;
        }
    }
    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
    {
        Console.WriteLine($"Not done: {ex.Message}");
    }
}

Console.WriteLine("Closing the database.");
return;

void AddScan()
{
    var tracking = Ask("Tracking number (example APX-DEMO-0001; start with FAIL to see a rejected upload)");
    var typeText = Ask("Scan type (Pickup, InTransit, Delivered, Failed)", "Pickup");
    if (!Enum.TryParse<ScanType>(typeText, ignoreCase: true, out var scanType) || !Enum.IsDefined(scanType))
    {
        Console.WriteLine("Unknown scan type.");
        return;
    }

    var courier = Ask("Courier id", "COURIER-001");
    var location = Ask("Location", "Demo Depot, Springfield");
    var notes = Ask("Notes (optional)");

    var scan = repository.Create(ShipmentScan.CreatePending(tracking, scanType, courier, location, notes));
    Console.WriteLine($"Saved locally: {scan.Id} (syncStatus = {scan.SyncStatus})");
}

void FindByTracking()
{
    var tracking = Ask("Tracking number");
    var scans = repository.FindByTrackingNumber(tracking);
    Console.WriteLine($"{scans.Count} scan(s) for '{tracking}', oldest first:");
    foreach (var scan in scans)
    {
        PrintScan(scan);
    }

    Console.WriteLine();
    Console.WriteLine("Query plan (it should name idx_scan_trackingNumber):");
    Console.WriteLine(repository.ExplainFindByTrackingNumber());
}

void ListPending()
{
    var scans = repository.ListPending();
    Console.WriteLine($"{scans.Count} pending scan(s):");
    foreach (var scan in scans)
    {
        PrintScan(scan);
    }
}

async Task SimulateSyncAsync()
{
    Console.WriteLine("Uploading pending scans to the fake uploader...");
    var result = await outbox.ProcessPendingAsync();
    Console.WriteLine($"Attempted {result.Attempted}: Synced {result.Synced}, Failed {result.Failed}.");
}

void DeleteScan()
{
    var id = Ask("Scan id (scan::...)");
    Console.WriteLine(repository.Delete(id) ? $"Deleted {id}." : $"No scan with id {id}.");
}

void CountPerType()
{
    var courier = Ask("Courier id", "COURIER-001");
    var counts = repository.CountByScanType(courier);
    Console.WriteLine($"Scans per type for {courier}:");
    foreach (var scanType in Enum.GetValues<ScanType>())
    {
        Console.WriteLine($"  {scanType,-10} {counts.GetValueOrDefault(scanType)}");
    }
}

static string Ask(string prompt, string? defaultValue = null)
{
    Console.Write(defaultValue is null ? $"{prompt}: " : $"{prompt} [{defaultValue}]: ");
    var input = Console.ReadLine()?.Trim();
    return string.IsNullOrEmpty(input) ? defaultValue ?? string.Empty : input;
}

static void PrintScan(ShipmentScan scan) =>
    Console.WriteLine(
        $"  {scan.Id} | {scan.TrackingNumber} | {scan.ScanType} | {scan.CourierId} | {scan.Location} | " +
        $"{scan.ScannedAtUtc:yyyy-MM-dd HH:mm:ss}Z | {scan.SyncStatus}" +
        (scan.Notes is null ? string.Empty : $" | {scan.Notes}"));
