namespace ApexPost.Courier.Core.Resilience;

/// <summary>
/// The numbers of the resilience pipeline (see RESILIENCE.md for why each one was chosen). The defaults are for a courier on a mobile
/// network; tests use tiny values.
/// </summary>
public sealed record ResilienceOptions
{
    /// <summary>Tries after the first one. 3 means up to 4 requests for one call.</summary>
    public int MaxRetryAttempts { get; init; } = 3;

    /// <summary>First wait before a retry. The wait doubles each time and gets random jitter: about 0.5 s, 1 s, 2 s.</summary>
    public TimeSpan RetryBaseDelay { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <summary>One request may take this long before it is cancelled (and then retried).</summary>
    public TimeSpan AttemptTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>The whole call, with all its retries and waits, may take this long.</summary>
    public TimeSpan TotalTimeout { get; init; } = TimeSpan.FromSeconds(45);

    /// <summary>The circuit opens when at least this share of the calls in the sampling window failed...</summary>
    public double CircuitFailureRatio { get; init; } = 0.5;

    /// <summary>...and at least this many calls were made in the window.</summary>
    public int CircuitMinimumThroughput { get; init; } = 4;

    public TimeSpan CircuitSamplingDuration { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>How long the circuit stays open before one call is let through to test the server.</summary>
    public TimeSpan CircuitBreakDuration { get; init; } = TimeSpan.FromSeconds(20);
}
