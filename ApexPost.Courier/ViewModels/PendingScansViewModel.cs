using System.Collections.ObjectModel;
using ApexPost.Courier.Core.Sync;
using ApexPost.Courier.Offline.Abstractions;
using ApexPost.Courier.Offline.Domain;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ApexPost.Courier.ViewModels;

/// <summary>One line of the list.</summary>
public sealed record ScanRow(string Title, string Detail, string Status, Color StatusColor);

/// <summary>Scans that are not on the server yet: Pending (waiting to be sent) and Failed (the server refused them).</summary>
public partial class PendingScansViewModel : ObservableObject
{
    private readonly IScanRepository _repository;

    public PendingScansViewModel(IScanRepository repository, SyncCoordinator coordinator)
    {
        _repository = repository;
        coordinator.Changed += (_, _) => MainThread.BeginInvokeOnMainThread(Refresh);
    }

    [ObservableProperty]
    private string _summary = string.Empty;

    public ObservableCollection<ScanRow> Scans { get; } = [];

    public void Refresh()
    {
        var open = _repository.ListAll().Where(scan => scan.SyncStatus != SyncStatus.Synced).ToList();

        Scans.Clear();
        foreach (var scan in open)
        {
            var failed = scan.SyncStatus == SyncStatus.Failed;
            Scans.Add(new ScanRow(
                $"{scan.TrackingNumber} · {scan.ScanType}",
                $"{scan.Location} · {scan.ScannedAtUtc.ToLocalTime():dd MMM HH:mm}" + (scan.RecipientSignatureName is null ? "" : $" · signed: {scan.RecipientSignatureName}"),
                failed ? "Refused by the server (will not be sent again)" : "Waiting to be sent",
                failed ? Color.FromArgb("#B3261E") : Color.FromArgb("#8A5A00")));
        }

        var pending = open.Count(scan => scan.SyncStatus == SyncStatus.Pending);
        Summary = $"{pending} waiting to be sent, {open.Count - pending} refused";
    }
}
