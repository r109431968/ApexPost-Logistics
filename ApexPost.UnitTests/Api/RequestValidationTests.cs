using System.ComponentModel.DataAnnotations;
using ApexPost.Api.Contracts;
using ApexPost.Domain.Parcels;

namespace ApexPost.UnitTests.Api;

/// <summary>
/// Boundary tests of the API request rules (week 5). No web server and no database: the same DataAnnotations
/// that ASP.NET Core runs before the controller are run directly. All dates are fixed, nothing depends on the clock.
/// </summary>
public class RequestValidationTests
{
    private static List<ValidationResult> Validate(object request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        return results;
    }

    private static CreateShipmentRequest Shipment(string tracking = "APX-12345678", decimal weightKg = 2.5m) => new()
    {
        TrackingNumber = tracking,
        WeightKg = weightKg,
        RecipientName = "Asha Verma",
        RecipientPhone = "+44 20 7946 0000",
        RecipientAddress = "14 Harbour Lane",
        RecipientCity = "Brightport"
    };

    private static ScanItemRequest Scan(ParcelScanType type, string? signature = null) => new()
    {
        ScanId = "scan::3f2504e0-4f89-41d3-9a0c-0305e82c3301",
        ScanType = type,
        Location = "Brightport depot",
        ScannedAtUtc = new DateTimeOffset(2026, 11, 10, 9, 30, 0, TimeSpan.Zero),
        RecipientSignatureName = signature
    };

    [Theory]
    [InlineData("APX-12345678", true)]
    [InlineData("APX-00000000", true)]
    [InlineData("APX-1234567", false)]      // 7 digits
    [InlineData("APX-123456789", false)]    // 9 digits
    [InlineData("apx-12345678", false)]     // lower case prefix
    [InlineData("APX12345678", false)]      // no dash
    [InlineData("APX-1234567A", false)]     // letter inside the digits
    [InlineData(" APX-12345678", false)]    // leading space
    [InlineData("", false)]
    public void TrackingNumber_MustBeApxDashEightDigits(string tracking, bool expectedValid)
    {
        var results = Validate(Shipment(tracking: tracking));

        Assert.Equal(expectedValid, results.Count == 0);
    }

    [Theory]
    [InlineData(0.1, true)]      // lowest allowed
    [InlineData(70.0, true)]     // highest allowed
    [InlineData(2.5, true)]
    [InlineData(0.09, false)]    // just under the lowest
    [InlineData(70.01, false)]   // just over the highest
    [InlineData(0.0, false)]
    [InlineData(-1.0, false)]
    public void WeightKg_MustBeBetweenPointOneAndSeventy(double weight, bool expectedValid)
    {
        var results = Validate(Shipment(weightKg: (decimal)weight));

        Assert.Equal(expectedValid, results.Count == 0);
    }

    [Fact]
    public void Shipment_WithoutRecipientName_IsInvalid()
    {
        var request = new CreateShipmentRequest
        {
            TrackingNumber = "APX-12345678", WeightKg = 1m, RecipientName = "",
            RecipientPhone = "+44 20 7946 0000", RecipientAddress = "14 Harbour Lane", RecipientCity = "Brightport"
        };

        var results = Validate(request);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateShipmentRequest.RecipientName)));
    }

    [Theory]
    [InlineData(0, false)]       // an empty batch is pointless
    [InlineData(1, true)]        // smallest batch
    [InlineData(100, true)]      // largest batch
    [InlineData(101, false)]     // one too many
    public void ScanBatch_MustHaveBetweenOneAndOneHundredScans(int count, bool expectedValid)
    {
        var batch = new ScanUploadRequest
        {
            CourierId = "courier-007",
            Scans = Enumerable.Range(0, count).Select(_ => Scan(ParcelScanType.InTransit)).ToList()
        };

        var results = Validate(batch);

        Assert.Equal(expectedValid, results.Count == 0);
    }

    [Fact]
    public void DeliveredScan_WithoutSignatureName_IsInvalid()
    {
        var results = Validate(Scan(ParcelScanType.Delivered, signature: null));

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(ScanItemRequest.RecipientSignatureName)));
    }

    [Fact]
    public void DeliveredScan_WithSignatureName_IsValid()
    {
        var results = Validate(Scan(ParcelScanType.Delivered, signature: "Asha Verma"));

        Assert.Empty(results);
    }

    [Theory]
    [InlineData(ParcelScanType.Pickup)]
    [InlineData(ParcelScanType.InTransit)]
    [InlineData(ParcelScanType.Failed)]
    public void NonDeliveredScan_DoesNotNeedASignatureName(ParcelScanType type)
    {
        var results = Validate(Scan(type, signature: null));

        Assert.Empty(results);
    }

    [Theory]
    [InlineData("scan::3f2504e0-4f89-41d3-9a0c-0305e82c3301", true)]
    [InlineData("3f2504e0-4f89-41d3-9a0c-0305e82c3301", false)]   // missing the scan:: prefix
    [InlineData("scan::not-a-guid", false)]
    public void ScanId_MustLookLikeScanColonColonGuid(string scanId, bool expectedValid)
    {
        var scan = new ScanItemRequest
        {
            ScanId = scanId, ScanType = ParcelScanType.InTransit, Location = "Depot",
            ScannedAtUtc = new DateTimeOffset(2026, 11, 10, 9, 30, 0, TimeSpan.Zero)
        };

        var results = Validate(scan);

        Assert.Equal(expectedValid, results.Count == 0);
    }
}
