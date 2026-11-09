namespace ApexPost.Courier.Core.Auth;

/// <summary>Where the access token is kept between runs. The app uses SecureStorage (Keychain / Keystore / DPAPI) behind this.</summary>
public interface ITokenStore
{
    Task<string?> GetAsync(CancellationToken cancellationToken = default);
    Task SetAsync(string token, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}

/// <summary>Keeps the token in memory only. For tests and for runs without a device store.</summary>
public sealed class InMemoryTokenStore : ITokenStore
{
    private string? _token;

    public Task<string?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(_token);

    public Task SetAsync(string token, CancellationToken cancellationToken = default)
    {
        _token = token;
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        _token = null;
        return Task.CompletedTask;
    }
}

/// <summary>Tells the app that the saved token was refused (401). The shell listens and shows the Login page.</summary>
public sealed class SessionEvents
{
    public event EventHandler? SessionExpired;

    public void RaiseSessionExpired() => SessionExpired?.Invoke(this, EventArgs.Empty);
}
