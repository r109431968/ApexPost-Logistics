using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;

namespace ApexPost.Api.Security;

public static class SecurityRegistration
{
    public const string PartnerPolicy = "Partner";
    public const string TokenRateLimit = "token";

    /// <summary>
    /// Everything about who may call the API:
    /// - JWT bearer authentication for couriers and admins (issuer, audience, lifetime and signature are all checked);
    /// - an API-key scheme that is used only by the partner route;
    /// - a fallback policy, so an endpoint without an explicit rule still requires a signed-in user (secure by default);
    /// - a rate limit on the sign-in endpoint.
    /// </summary>
    public static IServiceCollection AddApexPostSecurity(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var jwt = JwtSettings.From(configuration, environment);
        services.AddSingleton(jwt);
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<IDemoUserDirectory, DemoUserDirectory>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;       // keep the claim names exactly as in the token: sub, role, courierId
                options.RequireHttpsMetadata = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = jwt.SigningKey,
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],   // refuses "alg: none" and other algorithms
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "sub",
                    RoleClaimType = "role"
                };
            })
            .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, _ => { });

        services
            .AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(PartnerPolicy, policy => policy
                .AddAuthenticationSchemes(ApiKeyAuthenticationHandler.SchemeName)
                .RequireRole(ApiRoles.Partner));

        // 10 sign-in attempts per minute per address: slows down password guessing. Behind a proxy, forward the real client address.
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(TokenRateLimit, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });

        return services;
    }
}
