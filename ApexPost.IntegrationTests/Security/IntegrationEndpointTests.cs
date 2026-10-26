using System.Net;
using ApexPost.IntegrationTests.Support;

namespace ApexPost.IntegrationTests.Security;

public class IntegrationEndpointTests : IClassFixture<ApexFactory>
{
    private const string Url = "/integration/parcel-status/APX123456";
    private readonly HttpClient _client;

    public IntegrationEndpointTests(ApexFactory factory) => _client = factory.CreateBrowser();

    private static HttpRequestMessage Get(string? key = ApexFactory.ApiKey, string? ip = null, string? origin = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, Url);
        if (key is not null)
            request.Headers.Add("X-Api-Key", key);
        if (ip is not null)
            request.Headers.Add("X-Test-Ip", ip);
        if (origin is not null)
            request.Headers.Add("Origin", origin);
        return request;
    }

    [Fact]
    public async Task Without_an_api_key_the_endpoint_returns_401()
    {
        var response = await _client.SendAsync(Get(key: null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task With_a_wrong_api_key_the_endpoint_returns_401()
    {
        var response = await _client.SendAsync(Get(key: "not-the-key"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task From_a_blocked_ip_address_the_endpoint_returns_403_even_with_a_valid_key()
    {
        var response = await _client.SendAsync(Get(ip: "203.0.113.50"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task The_admin_pages_are_blocked_from_other_addresses_before_login_is_even_checked()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/Staff");
        request.Headers.Add("X-Test-Ip", "203.0.113.50");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}

public class ApiKeyNotConfiguredTests
{
    [Fact]
    public async Task When_no_key_is_configured_the_endpoint_returns_500_instead_of_letting_everyone_in()
    {
        using var factory = new ApexFactory(new Dictionary<string, string?>
        {
            ["Integration:ApiKeys:TestPartner"] = "",
            ["Integration:ApiKeys:CourierPartner"] = ""
        });
        var client = factory.CreateBrowser();
        var request = new HttpRequestMessage(HttpMethod.Get, "/integration/parcel-status/APX123456");
        request.Headers.Add("X-Api-Key", "anything");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }
}

public class CorsTests : IClassFixture<ApexFactory>
{
    private readonly HttpClient _client;

    public CorsTests(ApexFactory factory) => _client = factory.CreateBrowser();

    private static HttpRequestMessage Preflight(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/integration/parcel-status/APX123456");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "x-api-key");
        return request;
    }

    [Fact]
    public async Task The_approved_widget_origin_is_allowed_by_the_preflight()
    {
        var response = await _client.SendAsync(Preflight(ApexFactory.ApprovedOrigin));

        Assert.Equal(ApexFactory.ApprovedOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task Another_origin_gets_no_allow_origin_header()
    {
        var response = await _client.SendAsync(Preflight("https://evil.example"));

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task The_real_response_to_the_approved_origin_carries_the_allow_origin_header()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/integration/parcel-status/APX123456");
        request.Headers.Add("X-Api-Key", ApexFactory.ApiKey);
        request.Headers.Add("Origin", ApexFactory.ApprovedOrigin);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ApexFactory.ApprovedOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task CORS_does_not_block_a_non_browser_caller_which_is_why_the_api_key_is_still_needed()
    {
        // curl or Postman ignore CORS. A request from an unapproved origin still reaches the server and is
        // stopped only by the API key check.
        var request = new HttpRequestMessage(HttpMethod.Get, "/integration/parcel-status/APX123456");
        request.Headers.Add("Origin", "https://evil.example");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

public class RateLimitTests
{
    [Fact]
    public async Task The_login_endpoint_returns_429_once_the_limit_is_exceeded()
    {
        using var factory = new ApexFactory(new Dictionary<string, string?> { ["RateLimiting:LoginPermitLimit"] = "3" });
        var client = factory.CreateBrowser();
        var statuses = new List<HttpStatusCode>();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var response = await client.PostFormAsync(
                "/Account/Login", new Dictionary<string, string> { ["Email"] = "x@y.test", ["Password"] = "wrong" });
            statuses.Add(response.StatusCode);
        }

        Assert.DoesNotContain(HttpStatusCode.TooManyRequests, statuses.Take(3));
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[3]);
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[4]);
    }

    [Fact]
    public async Task A_429_response_says_when_to_retry()
    {
        using var factory = new ApexFactory(new Dictionary<string, string?> { ["RateLimiting:LoginPermitLimit"] = "1" });
        var client = factory.CreateBrowser();
        var fields = new Dictionary<string, string> { ["Email"] = "x@y.test", ["Password"] = "wrong" };

        await client.PostFormAsync("/Account/Login", fields);
        var blocked = await client.PostFormAsync("/Account/Login", fields);

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.True(blocked.Headers.Contains("Retry-After"));
    }

    [Fact]
    public async Task The_partner_endpoint_has_its_own_limit_per_partner()
    {
        using var factory = new ApexFactory(new Dictionary<string, string?> { ["RateLimiting:PartnerPermitLimit"] = "2" });
        var client = factory.CreateBrowser();
        var statuses = new List<HttpStatusCode>();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/integration/parcel-status/APX123456");
            request.Headers.Add("X-Api-Key", ApexFactory.ApiKey);
            statuses.Add((await client.SendAsync(request)).StatusCode);
        }

        Assert.Equal(
            new[] { HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests }, statuses.ToArray());
    }
}
