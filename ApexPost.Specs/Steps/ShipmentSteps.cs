using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using ApexPost.Specs.Support;
using Reqnroll;

namespace ApexPost.Specs.Steps;

[Binding]
public sealed class ShipmentSteps(ApiContext api)
{
    private async Task CreateAsync(string trackingNumber, string weightKg)
    {
        api.Response = await api.Http.PostAsJsonAsync("/api/v2/shipments", new
        {
            trackingNumber,
            weightKg = decimal.Parse(weightKg, CultureInfo.InvariantCulture),
            recipientName = "Asha Verma",
            recipientPhone = "+44 20 7946 0000",
            recipientAddress = "14 Harbour Lane",
            recipientCity = "Brightport"
        });
    }

    [Given("a shipment \"(.*)\" already exists")]
    public async Task GivenAShipmentAlreadyExists(string trackingNumber)
    {
        await CreateAsync(trackingNumber, "1.0");
        Assert.Equal(HttpStatusCode.Created, api.Response!.StatusCode);
    }

    [When("I create a shipment with tracking number \"(.*)\" and weight (.*) kg")]
    public Task WhenICreateAShipment(string trackingNumber, string weightKg) => CreateAsync(trackingNumber, weightKg);

    [Then("the shipment \"(.*)\" can be read back")]
    public async Task ThenTheShipmentCanBeReadBack(string trackingNumber)
    {
        using var response = await api.Http.GetAsync($"/api/v2/shipments/{trackingNumber}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(trackingNumber, await response.Content.ReadAsStringAsync());
    }
}
