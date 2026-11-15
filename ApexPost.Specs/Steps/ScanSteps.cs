using System.Net.Http.Json;
using ApexPost.Specs.Support;
using Reqnroll;

namespace ApexPost.Specs.Steps;

[Binding]
public sealed class ScanSteps(ApiContext api)
{
    private sealed record UploadBody(int Accepted, int DuplicatesSkipped);

    private async Task UploadAsync(string courierId, string scanType, string scanId, string? signature, string trackingNumber)
    {
        api.LastScanId = scanId;
        api.Response = await api.Http.PostAsJsonAsync($"/api/v2/shipments/{trackingNumber}/scans", new
        {
            courierId,
            scans = new[]
            {
                new
                {
                    scanId,
                    scanType,
                    location = "Brightport hub",
                    scannedAtUtc = new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero),   // fixed date, not "now"
                    recipientSignatureName = string.IsNullOrWhiteSpace(signature) ? null : signature
                }
            }
        });
    }

    private static string NewScanId() => "scan::" + Guid.NewGuid();

    [Given("courier \"(.*)\" has uploaded an \"(.*)\" scan for shipment \"(.*)\"")]
    public async Task GivenCourierHasUploadedAScan(string courierId, string scanType, string trackingNumber)
    {
        await UploadAsync(courierId, scanType, NewScanId(), null, trackingNumber);
        Assert.True(api.Response!.IsSuccessStatusCode);
    }

    [When("courier \"(.*)\" uploads an \"(.*)\" scan for shipment \"(.*)\"")]
    public Task WhenCourierUploadsAScan(string courierId, string scanType, string trackingNumber) =>
        UploadAsync(courierId, scanType, NewScanId(), null, trackingNumber);

    [When("courier \"(.*)\" uploads the same scan again for shipment \"(.*)\"")]
    public Task WhenCourierUploadsTheSameScanAgain(string courierId, string trackingNumber)
    {
        Assert.NotNull(api.LastScanId);
        return UploadAsync(courierId, "InTransit", api.LastScanId!, null, trackingNumber);
    }

    [When("courier \"(.*)\" uploads a \"(.*)\" scan with signature name \"(.*)\" for shipment \"(.*)\"")]
    public Task WhenCourierUploadsAScanWithSignature(string courierId, string scanType, string signature, string trackingNumber) =>
        UploadAsync(courierId, scanType, NewScanId(), signature, trackingNumber);

    [Then("the upload reports (.*) accepted and (.*) duplicates skipped")]
    public async Task ThenTheUploadReports(int accepted, int duplicates)
    {
        var body = await api.Response!.Content.ReadFromJsonAsync<UploadBody>();
        Assert.Equal(new UploadBody(accepted, duplicates), body);
    }
}
