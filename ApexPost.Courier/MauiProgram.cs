using Microsoft.Extensions.DependencyInjection;
using System.Net.Security;
using ApexPost.Courier.Core.Auth;
using ApexPost.Courier.Core.Resilience;
using ApexPost.Courier.Core.Sync;
using ApexPost.Courier.Offline.Abstractions;
using ApexPost.Courier.Offline.Storage;
using ApexPost.Courier.Offline.Sync;
using ApexPost.Courier.Services;
using ApexPost.Courier.ViewModels;
using ApexPost.Courier.Views;
using Microsoft.Extensions.Logging;

namespace ApexPost.Courier;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var services = builder.Services;

        // The banner reads this; it is switched on by the circuit breaker and by failed calls. MainThread because the UI binds to it.
        var connection = new ConnectionStatus(action => MainThread.BeginInvokeOnMainThread(action));

        // Token in SecureStorage, typed API client with retry / circuit breaker / timeouts (all in ApexPost.Courier.Core).
        services.AddSingleton<ITokenStore, SecureStorageTokenStore>();
        var api = services.AddApexApi(new Uri(AppSettings.ApiBaseAddress), connection);

#if DEBUG
        // DEBUG ONLY, and only for the local development API: the ASP.NET Core development certificate is not trusted by the Android
        // emulator or by this app, and its name does not match 10.0.2.2. Certificate errors are accepted ONLY for localhost, 127.0.0.1
        // and 10.0.2.2. In a Release build this block does not exist, so certificate validation is never switched off there (see SECURITY.md).
        api.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (request, _, _, errors) =>
                errors == SslPolicyErrors.None
                || request.RequestUri?.Host is "localhost" or "127.0.0.1" or "10.0.2.2"
        });
#endif

        // Local store (Part A). The folder is explicit so the database is always in the app's own data folder.
        services.AddSingleton(_ => new CourierDatabase(Path.Combine(FileSystem.AppDataDirectory, "couchbase")));
        services.AddSingleton<IScanRepository>(sp => new CouchbaseScanRepository(sp.GetRequiredService<CourierDatabase>()));

        // Sync: batches of 20, started by SyncScheduler.
        services.AddSingleton(sp => new BatchSyncService(sp.GetRequiredService<IScanRepository>(), sp.GetRequiredService<IScanBatchUploader>()));
        services.AddSingleton<SyncCoordinator>();
        services.AddSingleton<SyncScheduler>();

        services.AddTransient<LoginViewModel>();
        services.AddTransient<NewScanViewModel>();
        services.AddTransient<PendingScansViewModel>();
        services.AddTransient<SyncStatusViewModel>();
        services.AddTransient<LoginPage>();
        services.AddTransient<NewScanPage>();
        services.AddTransient<PendingScansPage>();
        services.AddTransient<SyncStatusPage>();

        var app = builder.Build();
        ServiceHelper.Services = app.Services;
        return app;
    }
}
