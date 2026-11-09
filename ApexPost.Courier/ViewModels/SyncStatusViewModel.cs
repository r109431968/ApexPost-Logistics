using ApexPost.Courier.Core.Resilience;
using ApexPost.Courier.Core.Sync;
using ApexPost.Courier.Offline.Abstractions;
using ApexPost.Courier.Offline.Domain;
using ApexPost.Courier.Offline.Sync;
using ApexPost.Courier.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ApexPost.Courier.ViewModels;

public partial class SyncStatusViewModel : ObservableObject
{
    private readonly IScanRepository _repository;
    private readonly SyncCoordinator _coordinator;
    private readonly ConnectionStatus _connection;

    public SyncStatusViewModel(IScanRepository repository, SyncCoordinator coordinator, ConnectionStatus connection)
    {
        _repository = repository;
        _coordinator = coordinator;
        _connection = connection;

        coordinator.Changed += (_, _) => MainThread.BeginInvokeOnMainThread(Refresh);
        connection.PropertyChanged += (_, _) => MainThread.BeginInvokeOnMainThread(Refresh);
        Connectivity.Current.ConnectivityChanged += (_, _) => MainThread.BeginInvokeOnMainThread(Refresh);
    }

    [ObservableProperty]
    private string _networkText = string.Empty;

    [ObservableProperty]
    private string _serverText = string.Empty;

    [ObservableProperty]
    private string _countsText = string.Empty;

    [ObservableProperty]
    private string _lastRunText = "No sync yet in this run of the app.";

    [ObservableProperty]
    private string _lastResultText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRejections))]
    private string _rejectionsText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;

    public bool HasRejections => !string.IsNullOrEmpty(RejectionsText);

    public void Refresh()
    {
        NetworkText = Connectivity.Current.NetworkAccess == NetworkAccess.Internet
            ? "Network: connected"
            : "Network: no connection";

        ServerText = _connection.IsServerUnavailable
            ? $"Server: unavailable ({(_connection.IsCircuitOpen ? "circuit breaker open, calls paused" : "last call failed")})"
            : "Server: no problem seen";

        var all = _repository.ListAll();
        CountsText = $"Pending {all.Count(s => s.SyncStatus == SyncStatus.Pending)} · " +
                     $"Synced {all.Count(s => s.SyncStatus == SyncStatus.Synced)} · " +
                     $"Failed {all.Count(s => s.SyncStatus == SyncStatus.Failed)}";

        if (_coordinator.LastResult is { } result && _coordinator.LastRunAtUtc is { } at)
        {
            LastRunText = $"Last sync: {at.ToLocalTime():HH:mm:ss}";
            LastResultText = $"{result.Synced} sent, {result.Rejected} refused, {result.StillPending} still waiting in {result.Batches} batch(es). " +
                             result.StopReason switch
                             {
                                 SyncStopReason.Offline => "Stopped: the server could not be reached.",
                                 SyncStopReason.SessionExpired => "Stopped: the session ended, sign in again.",
                                 _ => "Finished."
                             };
            RejectionsText = result.Rejections.Count == 0
                ? string.Empty
                : "Refused: " + string.Join("; ", result.Rejections.Select(r => r.Reason ?? "no reason given"));
        }
    }

    [RelayCommand]
    private async Task SyncNowAsync()
    {
        IsBusy = true;
        try
        {
            await _coordinator.TriggerAsync();
        }
        finally
        {
            IsBusy = false;
            Refresh();
        }
    }
}
