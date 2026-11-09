using ApexPost.Courier.Core.Auth;
using ApexPost.Courier.Offline.Abstractions;
using ApexPost.Courier.Offline.Domain;
using ApexPost.Courier.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ApexPost.Courier.ViewModels;

public partial class NewScanViewModel(IScanRepository repository, AuthService auth, SyncScheduler scheduler) : ObservableObject
{
    [ObservableProperty]
    private string _trackingNumber = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDelivered))]
    private ScanType _selectedScanType = ScanType.InTransit;

    [ObservableProperty]
    private string _location = string.Empty;

    [ObservableProperty]
    private string _signatureName = string.Empty;

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    private string? _message;

    [ObservableProperty]
    private Color _messageColor = Colors.Green;

    [ObservableProperty]
    private string _courierText = string.Empty;

    public IReadOnlyList<ScanType> ScanTypes { get; } = Enum.GetValues<ScanType>();

    public bool IsDelivered => SelectedScanType == ScanType.Delivered;

    public bool HasMessage => !string.IsNullOrEmpty(Message);

    public void Refresh()
    {
        CourierText = auth.Current is { } session ? $"Courier {session.CourierId} ({session.UserName})" : "Not signed in";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var courierId = auth.Current?.CourierId;
        if (courierId is null)
        {
            Fail("You are not signed in.");
            return;
        }

        if (string.IsNullOrWhiteSpace(TrackingNumber))
        {
            Fail("Enter the tracking number.");
            return;
        }

        if (string.IsNullOrWhiteSpace(Location))
        {
            Fail("Enter the location.");
            return;
        }

        // The central system refuses a Delivered scan without the name of the person who signed. Better to ask now than to fail later, offline.
        if (IsDelivered && string.IsNullOrWhiteSpace(SignatureName))
        {
            Fail("A Delivered scan needs the name of the person who signed.");
            return;
        }

        try
        {
            var scan = ShipmentScan.CreatePending(
                TrackingNumber, SelectedScanType, courierId, Location, Notes,
                recipientSignatureName: IsDelivered ? SignatureName : null);
            repository.Create(scan);
        }
        catch (ArgumentException exception)
        {
            Fail(exception.Message);
            return;
        }

        Message = $"Saved on this device ({repository.CountPending()} waiting to be sent). It is sent when the server can be reached.";
        MessageColor = Colors.Green;
        TrackingNumber = string.Empty;
        SignatureName = string.Empty;
        Notes = string.Empty;

        await scheduler.RequestSyncAsync();   // does nothing harmful when offline: the scan simply stays Pending
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        await auth.SignOutAsync();
        await Shell.Current.GoToAsync("//login");
    }

    private void Fail(string text)
    {
        Message = text;
        MessageColor = Color.FromArgb("#B3261E");
    }
}
