using System.Net;
using ApexPost.Courier.Core.Api;
using ApexPost.Courier.Core.Auth;
using ApexPost.Courier.Core.Sync;
using ApexPost.Courier.Offline.Sync;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Polly.Timeout;

namespace ApexPost.Courier.Core.Resilience;

public static class ApexApiRegistration
{
    /// <summary>
    /// Registers the typed client with its pipeline. Calls go through, from the outside in:
    /// AuthHeaderHandler (token, 401) -> total timeout -> retry -> circuit breaker -> attempt timeout -> the network.
    /// The caller must register an <see cref="ITokenStore"/>. The returned builder lets the app change the primary handler
    /// (the Debug-only localhost certificate exception lives in the app, not here).
    /// </summary>
    public static IHttpClientBuilder AddApexApi(
        this IServiceCollection services, Uri baseAddress, ConnectionStatus connection, ResilienceOptions? options = null)
    {
        var settings = options ?? new ResilienceOptions();

        services.TryAddSingleton(connection);
        services.TryAddSingleton<SessionEvents>();
        services.TryAddTransient<AuthHeaderHandler>();
        services.TryAddSingleton<AuthService>();
        services.TryAddSingleton<IScanBatchUploader, ApiScanBatchUploader>();

        var httpClient = services
            .AddHttpClient<ApexApiClient>(client => client.BaseAddress = baseAddress)
            .AddHttpMessageHandler<AuthHeaderHandler>();

        httpClient.AddResilienceHandler("apex-api", (pipeline, context) =>
            {
                var logger = context.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("ApexPost.Resilience");

                // 1. The whole call, including every retry and every wait.
                pipeline.AddTimeout(new HttpTimeoutStrategyOptions { Name = "total-timeout", Timeout = settings.TotalTimeout });

                // 2. Retry only what can get better by waiting: network errors, timeouts, 408 and 5xx. Never a 4xx: the same request
                //    would get the same answer. Exponential backoff with jitter, so many phones do not retry at the same moment.
                //    Retrying a scan upload is safe because the server treats a repeated scan id as a duplicate (idempotent).
                pipeline.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = settings.MaxRetryAttempts,
                    Delay = settings.RetryBaseDelay,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    ShouldHandle = args => ValueTask.FromResult(IsTransient(args.Outcome)),
                    OnRetry = args =>
                    {
                        logger.LogWarning(
                            "Retry {Attempt} of {Max} in {Delay} ms because of {Reason}.",
                            args.AttemptNumber + 1, settings.MaxRetryAttempts, (int)args.RetryDelay.TotalMilliseconds, Describe(args.Outcome));
                        return default;
                    }
                });

                // 3. After repeated failures stop calling for a while. The banner is shown while the circuit is open.
                pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    FailureRatio = settings.CircuitFailureRatio,
                    MinimumThroughput = settings.CircuitMinimumThroughput,
                    SamplingDuration = settings.CircuitSamplingDuration,
                    BreakDuration = settings.CircuitBreakDuration,
                    ShouldHandle = args => ValueTask.FromResult(IsTransient(args.Outcome)),
                    OnOpened = args =>
                    {
                        logger.LogError("Circuit OPEN for {Seconds} s after: {Reason}.", (int)args.BreakDuration.TotalSeconds, Describe(args.Outcome));
                        connection.CircuitOpened(Describe(args.Outcome));
                        return default;
                    },
                    OnHalfOpened = _ =>
                    {
                        logger.LogInformation("Circuit HALF-OPEN: the next call tests the server.");
                        return default;
                    },
                    OnClosed = _ =>
                    {
                        logger.LogInformation("Circuit CLOSED: the server answers again.");
                        connection.CircuitClosed();
                        return default;
                    }
                });

                // 4. One request. If it hangs, it is cancelled (and the retry strategy above tries again).
                pipeline.AddTimeout(new HttpTimeoutStrategyOptions { Name = "attempt-timeout", Timeout = settings.AttemptTimeout });
            });

        return httpClient;
    }

    /// <summary>Network errors, timeouts, 408 and 5xx. Not 4xx, and not an open circuit.</summary>
    internal static bool IsTransient(Outcome<HttpResponseMessage> outcome) =>
        outcome.Exception is HttpRequestException or TimeoutRejectedException
        || outcome.Result is { } response && ((int)response.StatusCode == (int)HttpStatusCode.RequestTimeout || (int)response.StatusCode >= 500);

    private static string Describe(Outcome<HttpResponseMessage> outcome) =>
        outcome.Exception is { } exception ? exception.GetType().Name : $"HTTP {(int?)outcome.Result?.StatusCode}";
}
