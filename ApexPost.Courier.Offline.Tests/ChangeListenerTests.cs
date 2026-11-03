using ApexPost.Courier.Offline.Domain;
using ApexPost.Courier.Offline.Storage;

namespace ApexPost.Courier.Offline.Tests;

public class ChangeListenerTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    [Fact]
    public void A_new_scan_is_logged_with_its_id_and_the_live_pending_count()
    {
        using var store = new CourierStoreFixture();
        using var monitor = new PendingScanMonitor(store.Database, store.Repository, new StringWriter());

        var scan = store.Repository.Create(ShipmentScan.CreatePending("APX-DEMO-0001", ScanType.Pickup, "COURIER-001", "Demo Depot"));

        Assert.True(SpinWait.SpinUntil(
            () => monitor.Lines.Any(line => line.Contains(scan.Id) && line.EndsWith("pending scans: 1")), Wait));
    }

    [Fact]
    public void A_delete_is_logged_and_the_pending_count_goes_down()
    {
        using var store = new CourierStoreFixture();
        using var monitor = new PendingScanMonitor(store.Database, store.Repository, new StringWriter());
        var scan = store.Repository.Create(ShipmentScan.CreatePending("APX-DEMO-0001", ScanType.Pickup, "COURIER-001", "Demo Depot"));

        store.Repository.Delete(scan.Id);

        Assert.True(SpinWait.SpinUntil(
            () => monitor.Lines.Any(line => line.Contains(scan.Id) && line.EndsWith("pending scans: 0")), Wait));
    }

    [Fact]
    public async Task A_disposed_monitor_writes_nothing_more()
    {
        using var store = new CourierStoreFixture();
        var monitor = new PendingScanMonitor(store.Database, store.Repository, new StringWriter());
        monitor.Dispose();

        store.Repository.Create(ShipmentScan.CreatePending("APX-DEMO-0001", ScanType.Pickup, "COURIER-001", "Demo Depot"));
        await Task.Delay(500);

        Assert.Empty(monitor.Lines);
    }
}
