using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ApexPost.Specs.Support;

/// <summary>
/// Shared state of ONE scenario. Reqnroll creates a new instance for every scenario and hands the same instance
/// to every step class and hook of that scenario (context injection), so scenarios never share data.
/// </summary>
public sealed class ApiContext
{
    public SpecsApiFactory? Factory { get; private set; }
    public HttpClient? Client { get; private set; }
    public HttpResponseMessage? Response { get; set; }
    public string? LastScanId { get; set; }

    private sealed record TokenBody(string AccessToken);

    public void Start()
    {
        Factory = new SpecsApiFactory();
        Client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),   // https, so the HTTPS redirect does not answer first
            AllowAutoRedirect = false
        });
    }

    public HttpClient Http => Client ?? throw new InvalidOperationException("The API was not started for this scenario.");

    public async Task SignInAsync(string userName)
    {
        var response = await Http.PostAsJsonAsync("/api/v2/auth/token",
            new { userName, password = SpecsApiFactory.DemoPassword });
        response.EnsureSuccessStatusCode();

        var token = await response.Content.ReadFromJsonAsync<TokenBody>();
        Http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);
    }

    public void Stop()
    {
        Response?.Dispose();
        Client?.Dispose();
        Factory?.Dispose();   // drops the scenario's test database
    }
}
