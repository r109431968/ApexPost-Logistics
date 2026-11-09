using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ApexPost.Courier.Core.Resilience;

/// <summary>
/// "Can the app reach the server right now?" in one place, for the banner on every screen.
/// It is set to unavailable when the circuit breaker opens or a call fails because of the network or a broken server,
/// and back to available when a call gets an answer or the circuit closes again.
/// </summary>
public sealed class ConnectionStatus : INotifyPropertyChanged
{
    public const string BannerMessage = "Server unavailable – working offline";

    private readonly Action<Action> _dispatch;
    private bool _circuitOpen;
    private bool _lastCallFailed;
    private string? _lastProblem;

    /// <param name="dispatch">Runs an action on the UI thread. The app passes MainThread; tests and the console pass nothing.</param>
    public ConnectionStatus(Action<Action>? dispatch = null)
    {
        _dispatch = dispatch ?? (action => action());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>True while the banner must be shown.</summary>
    public bool IsServerUnavailable => _circuitOpen || _lastCallFailed;

    public bool IsCircuitOpen => _circuitOpen;

    public string BannerText => BannerMessage;

    /// <summary>The last problem in words, for the Sync Status screen.</summary>
    public string? LastProblem => _lastProblem;

    public void CircuitOpened(string? reason = null)
    {
        _circuitOpen = true;
        _lastProblem = reason ?? "The circuit breaker opened after repeated failures.";
        Raise();
    }

    public void CircuitClosed()
    {
        _circuitOpen = false;
        _lastCallFailed = false;
        Raise();
    }

    public void RequestFailed(string reason)
    {
        _lastCallFailed = true;
        _lastProblem = reason;
        Raise();
    }

    public void RequestSucceeded()
    {
        if (!_circuitOpen && !_lastCallFailed)
        {
            return;
        }

        _circuitOpen = false;
        _lastCallFailed = false;
        Raise();
    }

    private void Raise() => _dispatch(() =>
    {
        Notify(nameof(IsServerUnavailable));
        Notify(nameof(IsCircuitOpen));
        Notify(nameof(LastProblem));
    });

    private void Notify([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
