using ApexPost.Courier.Offline.Domain;

namespace ApexPost.Courier.Offline.Tests;

public class ShipmentScanTests
{
    [Fact]
    public void A_new_scan_is_pending_and_has_an_id_in_the_scan_guid_format()
    {
        var scan = ShipmentScan.CreatePending(" APX-DEMO-0001 ", ScanType.Pickup, "COURIER-001", "Demo Depot", "  fragile  ");

        Assert.Equal(SyncStatus.Pending, scan.SyncStatus);
        Assert.True(ShipmentScan.IsValidId(scan.Id));
        Assert.StartsWith("scan::", scan.Id);
        Assert.Equal("APX-DEMO-0001", scan.TrackingNumber);
        Assert.Equal("fragile", scan.Notes);
        Assert.Equal(TimeSpan.Zero, scan.ScannedAtUtc.Offset);
    }

    [Theory]
    [InlineData("scan::not-a-guid")]
    [InlineData("12345678-1234-1234-1234-123456789012")]
    [InlineData("")]
    [InlineData(null)]
    public void Ids_that_are_not_scan_guid_are_rejected(string? id) => Assert.False(ShipmentScan.IsValidId(id));

    [Fact]
    public void A_blank_tracking_number_is_rejected() =>
        Assert.Throws<ArgumentException>(() => ShipmentScan.CreatePending(" ", ScanType.Pickup, "COURIER-001", "Demo Depot"));

    [Fact]
    public void Blank_notes_are_stored_as_no_notes()
    {
        var scan = ShipmentScan.CreatePending("APX-DEMO-0002", ScanType.Delivered, "COURIER-001", "Demo Depot", "   ");

        Assert.Null(scan.Notes);
    }
}
