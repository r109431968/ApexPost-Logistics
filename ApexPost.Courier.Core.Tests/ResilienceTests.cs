using System.Net;
using ApexPost.Courier.Core.Api;
using ApexPost.Courier.Core.Resilience;

namespace ApexPost.Courier.Core.Tests;

/// <summary>The real pipeline (retry, circuit breaker, timeouts) running on a fake network.</summary>
public class ResilienceTests
{
    private static readonly IReadOnlyList<ScanItemDto> Scans =
        [new("scan::3f2504e0-4f89-41d3-9a0c-0305e82c3301", "Pickup", "Depot", DateTimeOffset.UtcNow, null, null)];

    private static Task<ApiResult<ScanUploadResponseDto>> Upload(Harness harness) =>
        harness.Client.UploadScansAsync("APX-12345678", "courier-007", Scans);

    [Fact]
    public async Task Two_server_errors_then_success_are_retried_and_the_call_succeeds()
    {
        var network = new StubHandler((_, call) => Task.FromResult(call < 3
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : Http.Json(HttpStatusCode.OK, Http.UploadOk)));
        using var harness = new Harness(network);

        var result = await Upload(harness);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, network.Calls);
    }

    [Fact]
    public async Task A_timeout_status_408_is_retried()
    {
        var network = new StubHandler((_, call) => Task.FromResult(call == 1
            ? new HttpResponseMessage(HttpStatusCode.RequestTimeout)
            : Http.Json(HttpStatusCode.OK, Http.UploadOk)));
        using var harness = new Harness(network);

        Assert.True((await Upload(harness)).IsSuccess);
        Assert.Equal(2, network.Calls);
    }

    [Fact]
    public async Task A_network_error_is_retried_three_times_then_reported_as_unreachable()
    {
        var network = new StubHandler((_, _) => throw new HttpRequestException("no route to host"));
        using var harness = new Harness(network);

        var result = await Upload(harness);

        Assert.Equal(ApiFailureKind.Unreachable, result.Error!.Kind);
        Assert.True(result.Error.IsTransient);
        Assert.Equal(4, network.Calls);   // the first try + 3 retries
        Assert.True(harness.Connection.IsServerUnavailable);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Conflict)]
    public async Task Client_errors_are_never_retried(HttpStatusCode status)
    {
        var network = new StubHandler((_, _) => Task.FromResult(Http.Problem(status, "No")));
        using var harness = new Harness(network);

        await Upload(harness);

        Assert.Equal(1, network.Calls);
    }

    [Fact]
    public async Task A_persistent_500_is_retried_and_reported_as_a_server_error()
    {
        var network = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        using var harness = new Harness(network);

        var result = await Upload(harness);

        Assert.Equal(ApiFailureKind.Server, result.Error!.Kind);
        Assert.Equal(4, network.Calls);
        Assert.True(harness.Connection.IsServerUnavailable);
    }

    [Fact]
    public async Task A_request_that_hangs_is_cancelled_by_the_attempt_timeout_and_retried()
    {
        var options = Harness.FastOptions with { AttemptTimeout = TimeSpan.FromMilliseconds(100), MaxRetryAttempts = 1 };
        var network = new DelayHandler(call => call == 1 ? TimeSpan.FromSeconds(10) : TimeSpan.Zero);   // the first call never answers in time
        using var harness = new Harness(network, options);

        var result = await Upload(harness);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, network.Calls);
    }

    [Fact]
    public async Task Without_an_answer_the_total_timeout_ends_the_whole_call()
    {
        var options = Harness.FastOptions with { AttemptTimeout = TimeSpan.FromSeconds(5), TotalTimeout = TimeSpan.FromMilliseconds(200) };
        var network = new DelayHandler(_ => TimeSpan.FromSeconds(10));
        using var harness = new Harness(network, options);

        var result = await Upload(harness);

        Assert.Equal(ApiFailureKind.Timeout, result.Error!.Kind);
        Assert.Equal(1, network.Calls);
    }

    [Fact]
    public async Task After_repeated_failures_the_circuit_opens_and_calls_stop_reaching_the_network()
    {
        var options = Harness.FastOptions with
        {
            MaxRetryAttempts = 3,
            CircuitMinimumThroughput = 2,
            CircuitFailureRatio = 0.5,
            CircuitBreakDuration = TimeSpan.FromSeconds(30)
        };
        var network = new StubHandler((_, _) => throw new HttpRequestException("down"));
        using var harness = new Harness(network, options);

        var first = await Upload(harness);
        var callsAfterFirst = network.Calls;
        var second = await Upload(harness);

        Assert.Equal(ApiFailureKind.CircuitOpen, first.Error!.Kind);   // the breaker opened while the retries were going on
        Assert.True(callsAfterFirst < 4);                              // so not all 4 tries reached the network
        Assert.Equal(ApiFailureKind.CircuitOpen, second.Error!.Kind);
        Assert.Equal(callsAfterFirst, network.Calls);                  // the second call never reached the network
        Assert.True(harness.Connection.IsCircuitOpen);
        Assert.True(harness.Connection.IsServerUnavailable);
    }

    [Fact]
    public async Task When_the_server_comes_back_the_circuit_closes_and_the_banner_goes_away()
    {
        var options = Harness.FastOptions with
        {
            MaxRetryAttempts = 1,
            CircuitMinimumThroughput = 2,
            CircuitFailureRatio = 0.5,
            CircuitBreakDuration = TimeSpan.FromMilliseconds(500)
        };
        var serverIsUp = false;
        var network = new StubHandler((_, _) => serverIsUp
            ? Task.FromResult(Http.Json(HttpStatusCode.OK, Http.UploadOk))
            : throw new HttpRequestException("down"));
        using var harness = new Harness(network, options);

        await Upload(harness);   // the first try and its one retry both fail: the circuit opens
        Assert.True(harness.Connection.IsCircuitOpen);

        serverIsUp = true;
        await Task.Delay(TimeSpan.FromMilliseconds(700));   // the break time is over: the next call is the test call
        var result = await Upload(harness);

        Assert.True(result.IsSuccess);
        Assert.False(harness.Connection.IsCircuitOpen);
        Assert.False(harness.Connection.IsServerUnavailable);
    }

    [Fact]
    public void The_banner_text_is_the_one_in_the_task()
    {
        Assert.Equal("Server unavailable – working offline", new ConnectionStatus().BannerText);
    }

    [Fact]
    public void ConnectionStatus_tells_listeners_and_uses_the_dispatcher()
    {
        var dispatched = 0;
        var status = new ConnectionStatus(action => { dispatched++; action(); });
        var changed = new List<string?>();
        status.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        status.RequestFailed("down");

        Assert.True(status.IsServerUnavailable);
        Assert.Equal(1, dispatched);
        Assert.Contains(nameof(ConnectionStatus.IsServerUnavailable), changed);

        status.RequestSucceeded();
        Assert.False(status.IsServerUnavailable);
    }
}
