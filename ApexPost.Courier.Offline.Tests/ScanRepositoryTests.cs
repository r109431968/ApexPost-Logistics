using ApexPost.Courier.Offline.Domain;
using ApexPost.Courier.Offline.Storage;

namespace ApexPost.Courier.Offline.Tests;

public class ScanRepositoryTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 3, 8, 0, 0, TimeSpan.Zero);

    private static ShipmentScan NewScan(
        string tracking = "APX-DEMO-0001",
        ScanType type = ScanType.Pickup,
        string courier = "COURIER-001",
        int minutes = 0,
        string? notes = null) =>
        ShipmentScan.CreatePending(tracking, type, courier, "Demo Depot", notes, Start.AddMinutes(minutes));

    [Fact]
    public void A_created_scan_can_be_read_back_by_id()
    {
        using var store = new CourierStoreFixture();
        var scan = NewScan(notes: "left at front desk");

        store.Repository.Create(scan);
        var loaded = store.Repository.GetById(scan.Id);

        Assert.Equal(scan, loaded);
    }

    [Fact]
    public void A_new_scan_is_always_saved_as_pending()
    {
        using var store = new CourierStoreFixture();
        var scan = NewScan() with { SyncStatus = SyncStatus.Synced };

        var saved = store.Repository.Create(scan);

        Assert.Equal(SyncStatus.Pending, saved.SyncStatus);
        Assert.Equal(SyncStatus.Pending, store.Repository.GetById(scan.Id)!.SyncStatus);
    }

    [Fact]
    public void Creating_the_same_id_twice_is_rejected()
    {
        using var store = new CourierStoreFixture();
        var scan = NewScan();
        store.Repository.Create(scan);

        Assert.Throws<InvalidOperationException>(() => store.Repository.Create(scan));
    }

    [Fact]
    public void An_unknown_id_returns_null()
    {
        using var store = new CourierStoreFixture();

        Assert.Null(store.Repository.GetById(ShipmentScan.NewId()));
    }

    [Fact]
    public void Update_changes_the_stored_fields_and_reports_a_missing_scan()
    {
        using var store = new CourierStoreFixture();
        var scan = store.Repository.Create(NewScan(notes: "first note"));

        var changed = scan with { Location = "Demo Hub", Notes = null, ScanType = ScanType.InTransit };

        Assert.True(store.Repository.Update(changed));
        Assert.Equal(changed, store.Repository.GetById(scan.Id));
        Assert.False(store.Repository.Update(NewScan(tracking: "APX-DEMO-9999")));
    }

    [Fact]
    public void Delete_removes_the_scan_and_reports_a_missing_scan()
    {
        using var store = new CourierStoreFixture();
        var scan = store.Repository.Create(NewScan());

        Assert.True(store.Repository.Delete(scan.Id));
        Assert.Null(store.Repository.GetById(scan.Id));
        Assert.False(store.Repository.Delete(scan.Id));
    }

    [Fact]
    public void ListAll_returns_every_scan_oldest_first()
    {
        using var store = new CourierStoreFixture();
        var late = store.Repository.Create(NewScan("APX-DEMO-0001", minutes: 30));
        var early = store.Repository.Create(NewScan("APX-DEMO-0002", minutes: 5));

        var all = store.Repository.ListAll();

        Assert.Equal(new[] { early.Id, late.Id }, all.Select(s => s.Id));
    }

    [Fact]
    public void Scans_of_one_tracking_number_come_back_in_time_order()
    {
        using var store = new CourierStoreFixture();
        var delivered = store.Repository.Create(NewScan("APX-DEMO-0001", ScanType.Delivered, minutes: 90));
        var pickup = store.Repository.Create(NewScan("APX-DEMO-0001", ScanType.Pickup, minutes: 0));
        var transit = store.Repository.Create(NewScan("APX-DEMO-0001", ScanType.InTransit, minutes: 45));
        store.Repository.Create(NewScan("APX-DEMO-0002", minutes: 10));

        var scans = store.Repository.FindByTrackingNumber("APX-DEMO-0001");

        Assert.Equal(new[] { pickup.Id, transit.Id, delivered.Id }, scans.Select(s => s.Id));
    }

    [Fact]
    public void A_tracking_number_with_query_syntax_is_only_a_value_never_part_of_the_query()
    {
        using var store = new CourierStoreFixture();
        store.Repository.Create(NewScan("APX-DEMO-0001"));

        var scans = store.Repository.FindByTrackingNumber("x' OR trackingNumber LIKE '%");

        Assert.Empty(scans);
        Assert.Single(store.Repository.ListAll());
    }

    [Fact]
    public void Only_pending_scans_are_listed_as_pending()
    {
        using var store = new CourierStoreFixture();
        var first = store.Repository.Create(NewScan("APX-DEMO-0001", minutes: 0));
        var second = store.Repository.Create(NewScan("APX-DEMO-0002", minutes: 1));
        store.Repository.Create(NewScan("APX-DEMO-0003", minutes: 2));
        store.Repository.Update(second.MarkSynced());

        var pending = store.Repository.ListPending();

        Assert.Equal(2, pending.Count);
        Assert.DoesNotContain(pending, s => s.Id == second.Id);
        Assert.Equal(first.Id, pending[0].Id);
        Assert.Equal(2, store.Repository.CountPending());
    }

    [Fact]
    public void Scans_are_counted_per_scan_type_for_one_courier_only()
    {
        using var store = new CourierStoreFixture();
        store.Repository.Create(NewScan("APX-DEMO-0001", ScanType.Pickup, "COURIER-001"));
        store.Repository.Create(NewScan("APX-DEMO-0002", ScanType.Pickup, "COURIER-001"));
        store.Repository.Create(NewScan("APX-DEMO-0003", ScanType.Delivered, "COURIER-001"));
        store.Repository.Create(NewScan("APX-DEMO-0004", ScanType.Pickup, "COURIER-002"));

        var counts = store.Repository.CountByScanType("COURIER-001");

        Assert.Equal(2, counts.Count);
        Assert.Equal(2, counts[ScanType.Pickup]);
        Assert.Equal(1, counts[ScanType.Delivered]);
    }

    [Fact]
    public void The_value_indexes_on_tracking_number_and_sync_status_exist()
    {
        using var store = new CourierStoreFixture();

        var indexes = store.Database.IndexNames();

        Assert.Contains(CourierDatabase.TrackingNumberIndexName, indexes);
        Assert.Contains(CourierDatabase.SyncStatusIndexName, indexes);
    }

    [Fact]
    public void Scans_survive_closing_and_reopening_the_database()
    {
        using var store = new CourierStoreFixture();
        var scan = store.Repository.Create(NewScan(notes: "saved while offline"));

        store.Database.Dispose();
        using var reopened = new CourierDatabase(store.DirectoryPath);
        var loaded = new CouchbaseScanRepository(reopened).GetById(scan.Id);

        Assert.Equal(scan, loaded);
    }
}
