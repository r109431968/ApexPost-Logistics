using System.Security.Claims;

namespace ApexPost.Api.Security;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// True when the caller may record scans for this courier: an Admin may act for anyone; a Courier only for the courierId in
    /// their own token. The check uses the token, never a value the caller sends.
    /// </summary>
    public static bool MayUploadFor(this ClaimsPrincipal user, string courierId)
    {
        if (user.IsInRole(ApiRoles.Admin))
            return true;

        return user.IsInRole(ApiRoles.Courier)
            && string.Equals(user.FindFirst(ApiClaims.CourierId)?.Value, courierId, StringComparison.Ordinal);
    }
}
