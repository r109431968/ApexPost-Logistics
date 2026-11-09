namespace ApexPost.Courier.Services;

/// <summary>
/// Where the API is. These are development addresses of the local API only; no production URL, key or password is in the app.
/// The Android emulator reaches the PC through 10.0.2.2.
/// </summary>
public static class AppSettings
{
    public static string ApiBaseAddress =>
#if ANDROID
        "https://10.0.2.2:7241/";
#else
        "https://localhost:7241/";
#endif

    /// <summary>How often the app tries to sync by itself. This is also how it notices that a server that was down is back.</summary>
    public static readonly TimeSpan PeriodicSyncInterval = TimeSpan.FromSeconds(30);
}
