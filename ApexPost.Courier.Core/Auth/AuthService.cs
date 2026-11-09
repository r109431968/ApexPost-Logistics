using ApexPost.Courier.Core.Api;

namespace ApexPost.Courier.Core.Auth;

/// <summary>Who is signed in. The courier id comes from the token and is used for every scan the courier records.</summary>
public sealed record CourierSession(string UserName, string CourierId, DateTimeOffset? ExpiresAtUtc);

/// <summary>Result of a sign-in attempt, ready to show.</summary>
public sealed record SignInOutcome(bool Succeeded, string? Message, ApiFailureKind? FailureKind);

/// <summary>
/// Sign-in, sign-out and "is there still a good token?". The token is kept in the <see cref="ITokenStore"/>; this class keeps the
/// parsed session in memory so the screens can read the courier id without touching storage.
/// </summary>
public sealed class AuthService
{
    private readonly ApexApiClient _api;
    private readonly ITokenStore _tokens;
    private readonly TimeProvider _time;

    public AuthService(ApexApiClient api, ITokenStore tokens, SessionEvents events, TimeProvider? time = null)
    {
        _api = api;
        _tokens = tokens;
        _time = time ?? TimeProvider.System;
        events.SessionExpired += (_, _) => Current = null;
    }

    public CourierSession? Current { get; private set; }

    public bool IsSignedIn => Current is not null;

    /// <summary>Raised when the session starts or ends. Can be raised on a background thread.</summary>
    public event EventHandler? Changed;

    public async Task<SignInOutcome> SignInAsync(string userName, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrEmpty(password))
        {
            return new SignInOutcome(false, "Enter the user name and the password.", ApiFailureKind.Validation);
        }

        var result = await _api.SignInAsync(userName.Trim(), password, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            var error = result.Error!;
            var message = error.Kind switch
            {
                ApiFailureKind.Unauthorized => "The user name or the password is wrong.",
                _ when error.IsTransient => "Cannot sign in now: the server is not reachable. Signing in needs a connection.",
                _ => error.Message
            };
            return new SignInOutcome(false, message, error.Kind);
        }

        var token = result.Value!.AccessToken;
        var info = JwtClaimReader.Read(token);
        if (info?.CourierId is null)
        {
            // An Admin (or an unreadable token) has no courier id, so it cannot record scans in this app.
            return new SignInOutcome(false, "This account is not a courier account. Sign in as a courier.", ApiFailureKind.Forbidden);
        }

        await _tokens.SetAsync(token, cancellationToken).ConfigureAwait(false);
        Current = new CourierSession(info.UserName ?? userName.Trim(), info.CourierId, info.ExpiresAtUtc);
        Changed?.Invoke(this, EventArgs.Empty);
        return new SignInOutcome(true, null, null);
    }

    /// <summary>At app start: use the saved token if it is still valid, otherwise the user must sign in.</summary>
    public async Task<bool> TryRestoreAsync(CancellationToken cancellationToken = default)
    {
        var token = await _tokens.GetAsync(cancellationToken).ConfigureAwait(false);
        var info = JwtClaimReader.Read(token);
        if (info?.CourierId is null || info.IsExpired(_time.GetUtcNow()))
        {
            await _tokens.ClearAsync(cancellationToken).ConfigureAwait(false);
            Current = null;
            return false;
        }

        Current = new CourierSession(info.UserName ?? info.CourierId, info.CourierId, info.ExpiresAtUtc);
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        await _tokens.ClearAsync(cancellationToken).ConfigureAwait(false);
        Current = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
