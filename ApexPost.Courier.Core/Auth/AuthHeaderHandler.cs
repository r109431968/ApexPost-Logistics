using System.Net;
using System.Net.Http.Headers;

namespace ApexPost.Courier.Core.Auth;

/// <summary>
/// Adds "Authorization: Bearer {token}" to every request. If the server answers 401 to a request that carried a token, the
/// token is no longer good: it is deleted and <see cref="SessionEvents.SessionExpired"/> is raised, so the app goes to the Login page.
/// The sign-in call itself is left alone (a wrong password also answers 401, and that must not look like an expired session).
/// </summary>
public sealed class AuthHeaderHandler(ITokenStore tokens, SessionEvents session) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var isSignIn = request.RequestUri?.AbsolutePath.EndsWith("/auth/token", StringComparison.OrdinalIgnoreCase) == true;
        var attached = false;

        if (!isSignIn && request.Headers.Authorization is null)
        {
            var token = await tokens.GetAsync(cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                attached = true;
            }
        }

        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (attached && response.StatusCode == HttpStatusCode.Unauthorized)
        {
            await tokens.ClearAsync(CancellationToken.None).ConfigureAwait(false);
            session.RaiseSessionExpired();
        }

        return response;
    }
}
