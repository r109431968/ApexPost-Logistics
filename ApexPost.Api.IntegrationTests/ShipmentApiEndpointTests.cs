using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ApexPost.Api.IntegrationTests;

/// <summary>
/// End-to-end tests of the version 2 API through the real pipeline: HTTP request, JWT check, controller, service,
/// EF Core and a real SQL Server test database. They prove that the parts work TOGETHER (routes, authentication,
/// JSON, the database). The rules inside the service are proven by the fast unit tests, not here.
/// </summary>
public class ShipmentApiEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private sealed record TokenBody(string AccessToken);

    private sealed record UploadBody(int Accepted, int DuplicatesSkipped);

    private HttpClient NewClient() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),   // https, so the HTTPS redirect does not answer first
        AllowAutoRedirect = false
    });

    private async Task<HttpClient> SignedInAsync(string userName)
    {
        var client = NewClient();

        var response = await client.PostAsJsonAsync("/api/v2/auth/token", new { userName, password = ApiFactory.DemoPassword });
        response.EnsureSuccessStatusCode();

        var token = await response.Content.ReadFromJsonAsync<TokenBody>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);
        return client;
    }

    private static object UploadBatch(string courierId, string scanId) => new
    {
        courierId,
        scans = new[]
        {
            new
            {
                scanId,
                scanType = "InTransit",
                location = "Brightport hub",
                scannedAtUtc = new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero)
            }
        }
    };

    [Fact]
    public async Task GetShipment_WithoutToken_Returns401()
    {
        // Arrange
        using var client = NewClient();

        // Act
        var response = await client.GetAsync("/api/v2/shipments/APX-10000001");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetShipment_SignedInAsCourier_Returns200WithTheShipment()
    {
        // Arrange
        using var client = await SignedInAsync("courier007");

        // Act
        var response = await client.GetAsync("/api/v2/shipments/APX-10000001");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("APX-10000001", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UploadScans_SameBatchTwice_SecondTimeIsSkippedAsDuplicate()
    {
        // Arrange
        using var client = await SignedInAsync("courier007");
        var batch = UploadBatch("courier-007", "scan::aaaaaaaa-1111-4111-8111-aaaaaaaaaaaa");

        // Act: the courier app retries after a dropped connection, so the same batch arrives twice.
        var first = await client.PostAsJsonAsync("/api/v2/shipments/APX-10000003/scans", batch);
        var second = await client.PostAsJsonAsync("/api/v2/shipments/APX-10000003/scans", batch);

        // Assert
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var firstBody = await first.Content.ReadFromJsonAsync<UploadBody>();
        var secondBody = await second.Content.ReadFromJsonAsync<UploadBody>();
        Assert.Equal(new UploadBody(1, 0), firstBody);
        Assert.Equal(new UploadBody(0, 1), secondBody);
    }

    [Fact]
    public async Task UploadScans_ForAnotherCourier_Returns403()
    {
        // Arrange: courier007 is signed in but the batch says it belongs to courier-008.
        using var client = await SignedInAsync("courier007");
        var batch = UploadBatch("courier-008", "scan::bbbbbbbb-2222-4222-8222-bbbbbbbbbbbb");

        // Act
        var response = await client.PostAsJsonAsync("/api/v2/shipments/APX-10000003/scans", batch);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
