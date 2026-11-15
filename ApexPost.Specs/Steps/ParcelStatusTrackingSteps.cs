using System.Net;
using System.Net.Http.Json;
using ApexPost.Specs.Support;
using Reqnroll;

namespace ApexPost.Specs.Steps;

[Binding]
public sealed class ParcelStatusTrackingSteps(ApiContext api)
{
    private sealed record ShipmentStatus(string Status);

    [When("shipment \"(.*)\" follows the journey \"(.*)\"")]
    public async Task WhenShipmentFollowsTheJourney(string trackingNumber, string journey)
    {
        foreach (var status in journey.Split(" then ", StringSplitOptions.TrimEntries))
        {
            api.Response?.Dispose();
            api.Response = await api.Http.PatchAsJsonAsync(
                $"/api/v2/shipments/{trackingNumber}/status",
                new { status });
            api.Response.EnsureSuccessStatusCode();
        }
    }

    [Then("shipment \"(.*)\" has status \"(.*)\"")]
    public async Task ThenShipmentHasStatus(string trackingNumber, string expectedStatus)
    {
        using var response = await api.Http.GetAsync($"/api/v2/shipments/{trackingNumber}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var shipment = await response.Content.ReadFromJsonAsync<ShipmentStatus>();
        Assert.Equal(expectedStatus, shipment?.Status);
    }
}
