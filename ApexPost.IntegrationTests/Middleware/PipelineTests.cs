using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApexPost.IntegrationTests.Support;

namespace ApexPost.IntegrationTests.Middleware;

public class PipelineTests : IClassFixture<ApexFactory>
{
    private readonly HttpClient _client;

    public PipelineTests(ApexFactory factory) => _client = factory.CreateBrowser();

    [Fact]
    public async Task Security_headers_are_present_on_every_response()
    {
        var response = await _client.GetAsync("/Account/Login");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("default-src 'self'", response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.True(response.Headers.Contains("Referrer-Policy"));
    }

    //[Fact]
    //public async Task Security_headers_are_also_present_on_error_responses()
    //{
    //    var response = await _client.GetAsync("/this/does/not/exist");

    //    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    //    Assert.True(response.Headers.Contains("X-Content-Type-Options"));
    //}

    [Fact]
    public async Task Security_headers_are_also_present_on_error_responses()
    {
        var response = await _client.GetAsync("/integration/parcel-status/APX123456");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Content-Type-Options"));
        Assert.True(response.Headers.Contains("Content-Security-Policy"));
    }

    [Fact]
    public async Task A_correlation_id_is_created_when_the_caller_sends_none()
    {
        var response = await _client.GetAsync("/Account/Login");

        var id = response.Headers.GetValues("X-Correlation-Id").Single();
        Assert.False(string.IsNullOrWhiteSpace(id));
    }

    [Fact]
    public async Task A_safe_incoming_correlation_id_is_reused_and_echoed()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/Account/Login");
        request.Headers.Add("X-Correlation-Id", "order-4711.abc");

        var response = await _client.SendAsync(request);

        Assert.Equal("order-4711.abc", response.Headers.GetValues("X-Correlation-Id").Single());
    }

    [Fact]
    public async Task An_unsafe_incoming_correlation_id_is_replaced()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/Account/Login");
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", "bad id <script>");

        var response = await _client.SendAsync(request);

        var id = response.Headers.GetValues("X-Correlation-Id").Single();
        Assert.DoesNotContain("script", id);
        Assert.DoesNotContain(" ", id);
    }

    [Fact]
    public async Task Response_time_header_is_added_by_the_timing_middleware()
    {
        var response = await _client.GetAsync("/Account/Login");

        Assert.True(response.Headers.Contains("X-Response-Time-Ms"));
    }

    [Fact]
    public async Task Health_endpoint_is_public_and_healthy()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_broken_upstream_returns_problem_details_with_a_trace_id_and_no_stack_trace()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/integration/parcel-status/FAIL0001");
        request.Headers.Add("X-Api-Key", ApexFactory.ApiKey);

        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(body);
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("traceId").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("correlationId").GetString()));
        Assert.DoesNotContain(" at ApexPost", body);
    }

    [Fact]
    public async Task A_slow_upstream_becomes_a_504_not_a_hang()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/integration/parcel-status/SLOW0001");
        request.Headers.Add("X-Api-Key", ApexFactory.ApiKey);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
    }

    [Fact]
    public async Task A_valid_partner_request_returns_the_parcel_status_as_json()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/integration/parcel-status/APX123456");
        request.Headers.Add("X-Api-Key", ApexFactory.ApiKey);

        var response = await _client.SendAsync(request);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("APX123456", result.GetProperty("trackingNumber").GetString());
    }
}

public class MaintenanceModeTests
{
    [Fact]
    public async Task Maintenance_mode_returns_503_with_Retry_After_but_keeps_health_available()
    {
        using var factory = new ApexFactory(new Dictionary<string, string?>
        {
            ["Maintenance:Enabled"] = "true",
            ["Maintenance:RetryAfterSeconds"] = "90"
        });
        var client = factory.CreateBrowser();

        var page = await client.GetAsync("/Account/Login");
        var health = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, page.StatusCode);
        Assert.Equal("90", page.Headers.GetValues("Retry-After").Single());
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }
}
