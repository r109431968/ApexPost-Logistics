using ApexPost.Courier.Offline.Abstractions;
using ApexPost.Courier.Offline.Domain;
using ApexPost.Courier.Offline.Sync;

namespace ApexPost.Courier.Offline.Tests;

/// <summary>Part A carry-overs that Part D needs: the signature name, and a network problem that is not a failure of the scan.</summary>
public class OfflineCarryOverTests
{
    private sealed class UnavailableUploader : IShipmentUploader
    {
        public int Calls { get; private set; }

        public Task<bool> UploadAsync(ShipmentScan scan, CancellationToken cancellationToken = default)
        {
            Calls++;
            throw new UploadUnavailableException("no network");
        }
    }

    [Fact]
    public void The_signature_name_is_trimmed_and_blank_means_none()
    {
        var signed = ShipmentScan.CreatePending("APX-DEMO-0001", ScanType.Delivered, "COURIER-001", "Door", recipientSignatureName: "  Asha Verma ");
        var blank = ShipmentScan.CreatePending("APX-DEMO-0002", ScanType.Delivered, "COURIER-001", "Door", recipientSignatureName: "   ");

        Assert.Equal("Asha Verma", signed.RecipientSignatureName);
        Assert.Null(blank.RecipientSignatureName);
    }

    [Fact]
    public void The_signature_name_is_saved_and_read_back_by_key_and_by_query()
    {
        using var store = new CourierStoreFixture();
        var scan = store.Repository.Create(
            ShipmentScan.CreatePending("APX-DEMO-0001", ScanType.Delivered, "COURIER-001", "Door", recipientSignatureName: "Asha Verma"));

        Assert.Equal("Asha Verma", store.Repository.GetById(scan.Id)!.RecipientSignatureName);   // key-value read
        Assert.Equal("Asha Verma", store.Repository.ListPending().Single().RecipientSignatureName);   // SQL++ read
    }

    [Fact]
    public void A_scan_without_a_signature_name_still_works_and_updating_can_remove_the_name()
    {
        using var store = new CourierStoreFixture();
        var scan = store.Repository.Create(
            ShipmentScan.CreatePending("APX-DEMO-0001", ScanType.Delivered, "COURIER-001", "Door", recipientSignatureName: "Asha Verma"));

        store.Repository.Update(scan with { RecipientSignatureName = null });

        Assert.Null(store.Repository.GetById(scan.Id)!.RecipientSignatureName);
    }

    [Fact]
    public async Task When_the_network_is_unavailable_the_scan_stays_pending_and_the_run_stops()
    {
        using var store = new CourierStoreFixture();
        var first = store.Repository.Create(ShipmentScan.CreatePending("APX-DEMO-0001", ScanType.Pickup, "COURIER-001", "Depot"));
        var second = store.Repository.Create(ShipmentScan.CreatePending("APX-DEMO-0002", ScanType.Pickup, "COURIER-001", "Depot"));
        var uploader = new UnavailableUploader();

        var result = await new OutboxService(store.Repository, uploader).ProcessPendingAsync();

        Assert.Equal(1, uploader.Calls);                                   // the second scan was not even tried
        Assert.Equal(0, result.Failed);                                    // offline is not a failure
        Assert.Equal(SyncStatus.Pending, store.Repository.GetById(first.Id)!.SyncStatus);
        Assert.Equal(SyncStatus.Pending, store.Repository.GetById(second.Id)!.SyncStatus);
    }
}
