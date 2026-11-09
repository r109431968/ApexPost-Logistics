using Microsoft.Extensions.DependencyInjection;
using ApexPost.Courier.Core.Auth;
using ApexPost.Courier.Services;

namespace ApexPost.Courier;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;

        // A 401 anywhere in the app: the token was cleared by the handler, now show the Login page.
        _services.GetRequiredService<SessionEvents>().SessionExpired += (_, _) =>
            MainThread.BeginInvokeOnMainThread(async () => await GoToLoginAsync());
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new AppShell()) { Title = "ApexPost Courier" };

        // Start the sync scheduler, and skip the Login page when a still valid token is saved.
        _services.GetRequiredService<SyncScheduler>().Start();
        _ = RestoreSessionAsync();
        return window;
    }

    private async Task RestoreSessionAsync()
    {
        var restored = await _services.GetRequiredService<AuthService>().TryRestoreAsync();
        if (restored)
        {
            await MainThread.InvokeOnMainThreadAsync(async () => await Shell.Current.GoToAsync("//main"));
        }
    }

    private static async Task GoToLoginAsync()
    {
        if (Shell.Current is not null)
        {
            await Shell.Current.GoToAsync("//login");
        }
    }
}
