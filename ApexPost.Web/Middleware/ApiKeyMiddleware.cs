using System.Security.Cryptography;
using System.Text;

namespace ApexPost.Web.Middleware;

/// <summary>Endpoint metadata: this endpoint needs a valid X-Api-Key header.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireApiKeyAttribute : Attribute;

/// <summary>
/// Keys live in configuration (Integration:ApiKeys:PartnerName), set with user-secrets or environment variables,
/// never in source. 401 for a missing or wrong key, 500 if no key is configured at all.
/// </summary>
public sealed class ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<ApiKeyMiddleware> logger)
{
    public const string HeaderName = "X-Api-Key";
    public const string PartnerItemKey = "ApexPost.Partner";
    private const string ConfigSection = "Integration:ApiKeys";

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<RequireApiKeyAttribute>() is null)
        {
            await next(context);
            return;
        }

        var configured = configuration.GetSection(ConfigSection).GetChildren()
            .Where(child => !string.IsNullOrWhiteSpace(child.Value))
            .Select(child => (Partner: child.Key, Key: child.Value!))
            .ToList();

        if (configured.Count == 0)
        {
            logger.LogError("No integration API key is configured ({Section}).", ConfigSection);
            await ProblemWriter.WriteAsync(
                context, StatusCodes.Status500InternalServerError,
                "Integration not configured", "The server has no integration API key configured.");
            return;
        }

        var supplied = context.Request.Headers[HeaderName].ToString();
        string? partner = null;

        if (!string.IsNullOrEmpty(supplied))
        {
            // Hash both sides so the comparison has a fixed length, then compare in constant time.
            // Every configured key is checked (no early exit) so timing does not reveal which one matched.
            var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
            foreach (var (name, key) in configured)
            {
                var keyHash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
                if (CryptographicOperations.FixedTimeEquals(suppliedHash, keyHash))
                    partner = name;
            }
        }

        if (partner is null)
        {
            logger.LogWarning("Rejected integration request with a missing or invalid API key.");
            context.Response.Headers.WWWAuthenticate = "ApiKey";
            await ProblemWriter.WriteAsync(
                context, StatusCodes.Status401Unauthorized, "Unauthorized", "A valid API key is required.");
            return;
        }

        context.Items[PartnerItemKey] = partner;
        await next(context);
    }
}
