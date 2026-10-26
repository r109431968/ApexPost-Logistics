using System.Net;

namespace ApexPost.Web.Middleware;

public enum IpZone
{
    Admin = 1,
    Integration = 2
}

/// <summary>Endpoint metadata: marks an endpoint as reachable only from the allowlist of its zone.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class IpRestrictedAttribute(IpZone zone) : Attribute
{
    public IpZone Zone { get; } = zone;
}

/// <summary>
/// Allowlist from configuration, fail closed: an empty list or an unknown address is denied with 403.
///
/// Proxy risk: this reads Connection.RemoteIpAddress. Behind a reverse proxy or load balancer that is the
/// PROXY's address, so either everyone looks allowed or everyone looks blocked. The fix is
/// UseForwardedHeaders with KnownProxies/KnownNetworks set. It must never trust X-Forwarded-For from the
/// open internet, because any client can send that header and pretend to be an allowed address.
/// </summary>
public sealed class IpRestrictionMiddleware(
    RequestDelegate next, SecuritySettings settings, ILogger<IpRestrictionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var rule = context.GetEndpoint()?.Metadata.GetMetadata<IpRestrictedAttribute>();
        if (rule is null)
        {
            await next(context);
            return;
        }

        var allowed = rule.Zone == IpZone.Admin ? settings.AdminAllowedIps : settings.IntegrationAllowedIps;
        var remote = context.Connection.RemoteIpAddress;

        if (remote is null || !IsAllowed(remote, allowed))
        {
            logger.LogWarning(
                "Blocked {Zone} request from {RemoteIp} to {Path}",
                rule.Zone, remote?.ToString() ?? "unknown", LogSanitiser.Clean(context.Request.Path.Value));

            await ProblemWriter.WriteAsync(
                context, StatusCodes.Status403Forbidden, "Forbidden", "Your address is not allowed to use this endpoint.");
            return;
        }

        await next(context);
    }

    private static bool IsAllowed(IPAddress remote, string[] allowList)
    {
        if (remote.IsIPv4MappedToIPv6)
            remote = remote.MapToIPv4();

        foreach (var entry in allowList)
        {
            if (string.IsNullOrWhiteSpace(entry))
                continue;

            var text = entry.Trim();

            if (text.Contains('/'))
            {
                if (IPNetwork.TryParse(text, out var network) && network.Contains(remote))
                    return true;
            }
            else if (IPAddress.TryParse(text, out var address))
            {
                if (address.IsIPv4MappedToIPv6)
                    address = address.MapToIPv4();
                if (address.Equals(remote))
                    return true;
            }
        }

        return false;
    }
}
