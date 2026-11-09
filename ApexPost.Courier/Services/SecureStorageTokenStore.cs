using ApexPost.Courier.Core.Auth;

namespace ApexPost.Courier.Services;

/// <summary>
/// Keeps the access token in SecureStorage (Android Keystore, Windows data protection). The token is the only secret the app holds.
/// If the device store cannot be used (for example some unpackaged Windows setups) the token stays in memory for this run only:
/// the courier signs in again next time, which is safe, just less convenient.
/// </summary>
public sealed class SecureStorageTokenStore : ITokenStore
{
    private const string Key = "apexpost.access_token";
    private string? _memory;

    public async Task<string?> GetAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await SecureStorage.Default.GetAsync(Key).ConfigureAwait(false) ?? _memory;
        }
        catch (Exception)
        {
            return _memory;
        }
    }

    public async Task SetAsync(string token, CancellationToken cancellationToken = default)
    {
        _memory = token;
        try
        {
            await SecureStorage.Default.SetAsync(Key, token).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Device store not available: memory only.
        }
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        _memory = null;
        try
        {
            SecureStorage.Default.Remove(Key);
        }
        catch (Exception)
        {
            // Nothing to remove.
        }

        return Task.CompletedTask;
    }
}
