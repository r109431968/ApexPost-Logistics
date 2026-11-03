using ApexPost.Courier.Offline.Abstractions;
using ApexPost.Courier.Offline.Domain;
using ApexPost.Courier.Offline.Sync;

namespace ApexPost.Courier.Offline.Tests;

public class OutboxServiceTests
{
    private static ShipmentScan NewScan(string tracking) =>
        ShipmentScan.CreatePending(tracking, ScanType.Pickup, "COURIER-001", "Demo Depot");

    [Fact]
    public async Task Accepted_scans_become_synced_and_rejected_scans_become_failed()
    {
        using var store = new CourierStoreFixture();
        var ok1 = store.Repository.Create(NewScan("APX-DEMO-0001"));
        var rejected = store.Repository.Create(NewScan("FAIL-DEMO-0002"));
        var ok2 = store.Repository.Create(NewScan("APX-DEMO-0003"));
        var outbox = new OutboxService(store.Repository, new FakeShipmentUploader());

        var result = await outbox.ProcessPendingAsync();

        Assert.Equal(new OutboxResult(3, 2, 1), result);
        Assert.Equal(SyncStatus.Synced, store.Repository.GetById(ok1.Id)!.SyncStatus);
        Assert.Equal(SyncStatus.Failed, store.Repository.GetById(rejected.Id)!.SyncStatus);
        Assert.Equal(SyncStatus.Synced, store.Repository.GetById(ok2.Id)!.SyncStatus);
        Assert.Equal(0, store.Repository.CountPending());
    }

    [Fact]
    public async Task A_second_run_has_nothing_left_to_send()
    {
        using var store = new CourierStoreFixture();
        store.Repository.Create(NewScan("APX-DEMO-0001"));
        var outbox = new OutboxService(store.Repository, new FakeShipmentUploader());
        await outbox.ProcessPendingAsync();

        var second = await outbox.ProcessPendingAsync();

        Assert.Equal(new OutboxResult(0, 0, 0), second);
    }

    [Fact]
    public async Task An_uploader_that_throws_marks_that_scan_failed_and_the_others_continue()
    {
        using var store = new CourierStoreFixture();
        var broken = store.Repository.Create(NewScan("BOOM-DEMO-0001"));
        var fine = store.Repository.Create(NewScan("APX-DEMO-0002"));
        var outbox = new OutboxService(store.Repository, new ThrowingForPrefixUploader("BOOM"));

        var result = await outbox.ProcessPendingAsync();

        Assert.Equal(new OutboxResult(2, 1, 1), result);
        Assert.Equal(SyncStatus.Failed, store.Repository.GetById(broken.Id)!.SyncStatus);
        Assert.Equal(SyncStatus.Synced, store.Repository.GetById(fine.Id)!.SyncStatus);
    }

    [Fact]
    public async Task A_cancelled_run_stops_and_leaves_the_scan_pending()
    {
        using var store = new CourierStoreFixture();
        var scan = store.Repository.Create(NewScan("APX-DEMO-0001"));
        var outbox = new OutboxService(store.Repository, new FakeShipmentUploader());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => outbox.ProcessPendingAsync(cancelled.Token));

        Assert.Equal(SyncStatus.Pending, store.Repository.GetById(scan.Id)!.SyncStatus);
    }

    private sealed class ThrowingForPrefixUploader(string prefix) : IShipmentUploader
    {
        public Task<bool> UploadAsync(ShipmentScan scan, CancellationToken cancellationToken = default) =>
            scan.TrackingNumber.StartsWith(prefix, StringComparison.Ordinal)
                ? throw new InvalidOperationException("The service is unreachable.")
                : Task.FromResult(true);
    }
}
