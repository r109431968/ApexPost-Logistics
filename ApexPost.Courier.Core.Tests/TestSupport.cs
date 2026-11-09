using System.Net;
using System.Text;
using ApexPost.Courier.Core.Api;
using ApexPost.Courier.Core.Auth;
using ApexPost.Courier.Core.Resilience;
using Microsoft.Extensions.DependencyInjection;

namespace ApexPost.Courier.Core.Tests;

/// <summary>A fake network: every request goes to the function, which also gets the number of the call (1, 2, 3...).</summary>
internal sealed class StubHandler(Func<HttpRequestMessage, int, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    private int _calls;

    public int Calls => _calls;

    public List<HttpRequestMessage> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return respond(request, Interlocked.Increment(ref _calls));
    }
}

/// <summary>A fake network that answers after a delay and honours cancellation, like a real socket. Needed to test the timeouts.</summary>
internal sealed class DelayHandler(Func<int, TimeSpan> delayFor) : HttpMessageHandler
{
    private int _calls;

    public int Calls => _calls;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var call = Interlocked.Increment(ref _calls);
        await Task.Delay(delayFor(call), cancellationToken);
        return Http.Json(HttpStatusCode.OK, Http.UploadOk);
    }
}

internal static class Http
{
    public static HttpResponseMessage Json(HttpStatusCode status, string json, string mediaType = "application/json") =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, mediaType) };

    public static HttpResponseMessage Problem(HttpStatusCode status, string title, string? detail = null, string? errorsJson = null)
    {
        var json = "{\"title\":\"" + title + "\",\"status\":" + (int)status;
        if (detail is not null)
        {
            json += ",\"detail\":\"" + detail + "\"";
        }

        if (errorsJson is not null)
        {
            json += ",\"errors\":" + errorsJson;
        }

        return Json(status, json + "}", "application/problem+json");
    }

    public const string UploadOk = """{"accepted":2,"duplicatesSkipped":0,"rejected":[]}""";
}

/// <summary>Builds the real pipeline (token handler, retry, circuit breaker, timeouts) on top of a fake network.</summary>
internal sealed class Harness : IDisposable
{
    private readonly ServiceProvider _provider;

    public Harness(HttpMessageHandler network, ResilienceOptions? options = null, string? token = null)
    {
        Connection = new ConnectionStatus();
        Tokens = new InMemoryTokenStore();
        if (token is not null)
        {
            Tokens.SetAsync(token).GetAwaiter().GetResult();
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ITokenStore>(Tokens);
        services.AddApexApi(new Uri("https://api.test/"), Connection, options ?? FastOptions).ConfigurePrimaryHttpMessageHandler(() => network);
        _provider = services.BuildServiceProvider();
    }

    /// <summary>Real policy, tiny waits. The circuit needs many calls to open, so only the circuit tests lower it.</summary>
    public static ResilienceOptions FastOptions { get; } = new()
    {
        RetryBaseDelay = TimeSpan.FromMilliseconds(1),
        CircuitMinimumThroughput = 100,
        AttemptTimeout = TimeSpan.FromSeconds(5),
        TotalTimeout = TimeSpan.FromSeconds(30)
    };

    public ConnectionStatus Connection { get; }
    public InMemoryTokenStore Tokens { get; }
    public ApexApiClient Client => _provider.GetRequiredService<ApexApiClient>();
    public SessionEvents Session => _provider.GetRequiredService<SessionEvents>();
    public AuthService Auth => _provider.GetRequiredService<AuthService>();
    public T Get<T>() where T : notnull => _provider.GetRequiredService<T>();

    public void Dispose() => _provider.Dispose();
}
