using System.Threading.RateLimiting;
using ApexPost.Application.Services;
using ApexPost.Web.Filters;
using ApexPost.Web.Health;
using ApexPost.Web.Middleware;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ApexPost.Web.Composition;

public static class WebServiceRegistration
{
    public const string PartnerCorsPolicy = "PartnerWidget";
    public const string LoginRateLimit = "login";
    public const string PartnerRateLimit = "partner";

    public static IServiceCollection AddApexPostWeb(this IServiceCollection services)
    {
        services.AddSingleton(sp => sp.GetRequiredService<IConfiguration>().GetSection(SecuritySettings.SectionName).Get<SecuritySettings>() ?? new SecuritySettings());
        services.AddSingleton(sp => sp.GetRequiredService<IConfiguration>().GetSection(RateLimitSettings.SectionName).Get<RateLimitSettings>() ?? new RateLimitSettings());
        services.AddSingleton(sp => sp.GetRequiredService<IConfiguration>().GetSection(PerformanceSettings.SectionName).Get<PerformanceSettings>() ?? new PerformanceSettings());
        services.AddOptions<MaintenanceOptions>().BindConfiguration(MaintenanceOptions.SectionName);

        // Global filters. A lower order number runs earlier (outermost): audit wraps performance wraps validation.
        services.AddControllersWithViews(options =>
        {
            options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
            options.Filters.Add<AuditActionFilter>(-1000);
            options.Filters.Add<PerformanceFilter>(-900);
            options.Filters.Add<ValidationFilter>(-500);
            options.Filters.Add<DomainExceptionFilter>();
        });

        services.AddAntiforgery(options =>
        {
            options.Cookie.Name = ".ApexPost.Antiforgery";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
        });

        services.AddAuthentication(AuthPolicies.CookieScheme)
            .AddCookie(AuthPolicies.CookieScheme, options =>
            {
                options.Cookie.Name = ".ApexPost.Auth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.LoginPath = "/Account/Login";
                options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
                options.SlidingExpiration = true;
                options.Events.OnValidatePrincipal = RevalidatePrincipalAsync;
                options.Events.OnRedirectToAccessDenied = DenyAccessAsync;
            });

        services.AddSingleton<IAuthorizationHandler, SameBranchHandler>();
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();

            options.AddPolicy(AuthPolicies.BranchManagerOnly, policy =>
                policy.RequireRole(nameof(StaffRole.BranchManager)));
            options.AddPolicy(AuthPolicies.CanTransact, policy =>
                policy.RequireRole(nameof(StaffRole.CounterClerk), nameof(StaffRole.BranchManager)));
            options.AddPolicy(AuthPolicies.CanViewReports, policy =>
                policy.RequireRole(nameof(StaffRole.BranchManager), nameof(StaffRole.Auditor), nameof(StaffRole.Administrator)));
            options.AddPolicy(AuthPolicies.AdministratorOnly, policy =>
                policy.RequireRole(nameof(StaffRole.Administrator)));
            options.AddPolicy(AuthPolicies.AuditViewers, policy =>
                policy.RequireRole(nameof(StaffRole.Auditor), nameof(StaffRole.Administrator)));
            options.AddPolicy(AuthPolicies.SameBranchOnly, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new SameBranchRequirement()));
        });

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(LoginRateLimit, http =>
            {
                var limits = http.RequestServices.GetRequiredService<RateLimitSettings>();
                return RateLimitPartition.GetFixedWindowLimiter(
                    RemoteAddressOf(http),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limits.LoginPermitLimit,
                        Window = TimeSpan.FromSeconds(limits.LoginWindowSeconds),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });

            options.AddPolicy(PartnerRateLimit, http =>
            {
                var limits = http.RequestServices.GetRequiredService<RateLimitSettings>();
                var partner = http.Items[ApiKeyMiddleware.PartnerItemKey] as string ?? RemoteAddressOf(http);
                return RateLimitPartition.GetFixedWindowLimiter(
                    "partner:" + partner,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limits.PartnerPermitLimit,
                        Window = TimeSpan.FromSeconds(limits.PartnerWindowSeconds),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });

            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);

                await ProblemWriter.WriteAsync(
                    context.HttpContext, StatusCodes.Status429TooManyRequests,
                    "Too many requests", "Please wait a moment and try again.");
            };
        });

        // CORS is built lazily from configuration. It tells browsers which origin may read the answer;
        // it does NOT stop anyone sending requests (curl, Postman and servers ignore it).
        services.AddOptions<CorsOptions>().Configure<SecuritySettings>((options, security) =>
            options.AddPolicy(PartnerCorsPolicy, policy => policy
                .WithOrigins(security.PartnerWidgetOrigins)
                .WithMethods("GET")
                .WithHeaders("X-Api-Key", "X-Correlation-Id")
                .WithExposedHeaders("X-Correlation-Id")));
        services.AddCors();

        services.AddHealthChecks().AddCheck<DataStoreHealthCheck>("data-store");

        return services;
    }

    private static string RemoteAddressOf(HttpContext http) =>
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    /// <summary>
    /// Runs on every request. A cookie issued to a staff member who has since been deactivated, or whose role
    /// or branch changed, stops working immediately instead of living on until it expires.
    /// </summary>
    private static async Task RevalidatePrincipalAsync(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        var staffId = principal?.StaffId;

        Staff? member = null;
        if (staffId is { } id)
        {
            var staffService = context.HttpContext.RequestServices.GetRequiredService<IStaffService>();
            member = await staffService.GetAsync(id, context.HttpContext.RequestAborted);
        }

        var stillValid = member is { IsActive: true }
            && principal is not null
            && member.Role == principal.ApexRole
            && string.Equals(member.HomeBranch.Value, principal.HomeBranchCode, StringComparison.Ordinal);

        if (!stillValid)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(AuthPolicies.CookieScheme);
        }
    }

    /// <summary>A signed-in user without the right role gets 403 (not a redirect), and the attempt is audited.</summary>
    private static async Task DenyAccessAsync(RedirectContext<CookieAuthenticationOptions> context)
    {
        var http = context.HttpContext;
        http.Response.StatusCode = StatusCodes.Status403Forbidden;

        var store = http.RequestServices.GetRequiredService<IAuditStore>();
        var clock = http.RequestServices.GetRequiredService<IClock>();
        var endpoint = http.GetEndpoint()?.DisplayName ?? http.Request.Path.Value ?? "unknown";

        try
        {
            await store.AppendAsync(new AuditEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = clock.UtcNow,
                StaffId = http.User.StaffId,
                Role = http.User.ApexRole?.ToString(),
                BranchCode = http.User.HomeBranchCode,
                Controller = "Security",
                Action = "AccessDenied",
                Operation = "AccessDenied",
                Details = new Dictionary<string, string> { ["endpoint"] = LogSanitiser.Clean(endpoint) },
                StatusCode = StatusCodes.Status403Forbidden,
                DurationMs = 0,
                CorrelationId = http.CorrelationId
            }, CancellationToken.None);
        }
        catch (IOException)
        {
            // The denial itself still stands even if the audit file cannot be written.
        }
    }
}
