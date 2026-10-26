using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Application.Exceptions;
using Microsoft.Extensions.Logging;

namespace ApexPost.Infrastructure.Shipping;

public sealed class CourierSettings
{
    public const string SectionName = "Courier";

    public int MaxConcurrentLookups { get; set; } = 3;
    public int TimeoutMilliseconds { get; set; } = 2000;
    public int SimulatedLatencyMilliseconds { get; set; } = 150;
}

/// <summary>
/// Pretends to be a slow external courier system. Used to show bounded concurrency (SemaphoreSlim),
/// timeouts, and cancellation handled separately from failure.
/// Tracking numbers starting with SLOW never answer in time, and FAIL always fails.
/// </summary>
public sealed class SimulatedCourierTrackingService : ICourierTrackingService, IDisposable
{
    private static readonly string[] Statuses =
        ["Label created", "Collected", "In transit", "Out for delivery", "Delivered"];

    private readonly SemaphoreSlim _slots;
    private readonly CourierSettings _settings;
    private readonly IClock _clock;
    private readonly ILogger<SimulatedCourierTrackingService> _logger;

    public SimulatedCourierTrackingService(
        CourierSettings settings, IClock clock, ILogger<SimulatedCourierTrackingService> logger)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(settings.MaxConcurrentLookups);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(settings.TimeoutMilliseconds);
        ArgumentOutOfRangeException.ThrowIfNegative(settings.SimulatedLatencyMilliseconds);

        _settings = settings;
        _clock = clock;
        _logger = logger;
        _slots = new SemaphoreSlim(settings.MaxConcurrentLookups, settings.MaxConcurrentLookups);
    }

    public async Task<TrackingResult> GetStatusAsync(string trackingNumber, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trackingNumber);

        var number = trackingNumber.Trim().ToUpperInvariant();
        if (number.Length is < 6 or > 24 || !number.All(char.IsAsciiLetterOrDigit))
            throw new ArgumentException("A tracking number has 6 to 24 letters or digits.", nameof(trackingNumber));

        var timeout = TimeSpan.FromMilliseconds(_settings.TimeoutMilliseconds);

        // The timeout covers waiting for a free slot as well as the lookup itself.
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        try
        {
            await _slots.WaitAsync(timeoutSource.Token);
            try
            {
                return await LookupAsync(number, timeoutSource.Token);
            }
            finally
            {
                _slots.Release();
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Our own timeout fired. A cancel requested by the caller is NOT caught here.
            _logger.LogWarning("Courier lookup timed out after {TimeoutMs} ms.", _settings.TimeoutMilliseconds);
            throw new ExternalServiceTimeoutException("Courier tracking", timeout);
        }
    }

    private async Task<TrackingResult> LookupAsync(string number, CancellationToken cancellationToken)
    {
        var delay = number.StartsWith("SLOW", StringComparison.Ordinal)
            ? TimeSpan.FromMinutes(1)
            : TimeSpan.FromMilliseconds(_settings.SimulatedLatencyMilliseconds);

        await Task.Delay(delay, cancellationToken);

        if (number.StartsWith("FAIL", StringComparison.Ordinal))
            throw new ExternalServiceException("Courier tracking is unavailable.");

        var index = number.Sum(character => (int)character) % Statuses.Length;
        return new TrackingResult(number, Statuses[index], _clock.UtcNow);
    }

    public void Dispose() => _slots.Dispose();
}
