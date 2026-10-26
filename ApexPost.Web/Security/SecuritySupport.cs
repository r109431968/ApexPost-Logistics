using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace ApexPost.Web.Security;

public sealed class SecuritySettings
{
    public const string SectionName = "Security";

    /// <summary>IPs or CIDR ranges allowed to reach admin pages. Empty means nobody (fail closed).</summary>
    public string[] AdminAllowedIps { get; set; } = [];

    /// <summary>IPs or CIDR ranges allowed to reach integration endpoints.</summary>
    public string[] IntegrationAllowedIps { get; set; } = [];

    /// <summary>The only browser origins allowed to call the parcel-status endpoint.</summary>
    public string[] PartnerWidgetOrigins { get; set; } = [];
}

public sealed class MaintenanceOptions
{
    public const string SectionName = "Maintenance";

    public bool Enabled { get; set; }

    public int RetryAfterSeconds { get; set; } = 120;
}

public sealed class RateLimitSettings
{
    public const string SectionName = "RateLimiting";

    public int LoginPermitLimit { get; set; } = 5;
    public int LoginWindowSeconds { get; set; } = 60;
    public int PartnerPermitLimit { get; set; } = 30;
    public int PartnerWindowSeconds { get; set; } = 60;
}

public sealed class PerformanceSettings
{
    public const string SectionName = "Performance";

    public int SlowActionMs { get; set; } = 500;
}

public static class ApexClaims
{
    public const string StaffId = "StaffId";
    public const string BranchCode = "BranchCode";

    /// <summary>The standard role claim, so [Authorize(Roles = ...)] and IsInRole also work.</summary>
    public const string Role = ClaimTypes.Role;
}

public static class AuthPolicies
{
    public const string CookieScheme = "ApexCookie";
    public const string BranchManagerOnly = "BranchManagerOnly";
    public const string SameBranchOnly = "SameBranchOnly";
    public const string CanTransact = "CanTransact";
    public const string CanViewReports = "CanViewReports";
    public const string AdministratorOnly = "AdministratorOnly";
    public const string AuditViewers = "AuditViewers";
}

/// <summary>C# 14 extension members: read the three claims as properties instead of repeating string lookups.</summary>
public static class ClaimsPrincipalApexExtensions
{
    extension(ClaimsPrincipal user)
    {
        public Guid? StaffId =>
            Guid.TryParse(user.FindFirst(ApexClaims.StaffId)?.Value, out var id) ? id : null;

        public string? HomeBranchCode => user.FindFirst(ApexClaims.BranchCode)?.Value;

        public StaffRole? ApexRole =>
            Enum.TryParse<StaffRole>(user.FindFirst(ApexClaims.Role)?.Value, out var role) ? role : null;

        /// <summary>Auditors and administrators are read-only roles that may look at any branch.</summary>
        public bool CanSeeAllBranches => user.ApexRole is StaffRole.Auditor or StaffRole.Administrator;
    }
}

public sealed class SameBranchRequirement : IAuthorizationRequirement;

/// <summary>
/// Succeeds when the requested branch (route or query value "branch") is the user's own branch.
/// Auditors and administrators are allowed everywhere because they never process sales.
/// </summary>
public sealed class SameBranchHandler : AuthorizationHandler<SameBranchRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, SameBranchRequirement requirement)
    {
        var home = context.User.HomeBranchCode;
        if (string.IsNullOrEmpty(home))
            return Task.CompletedTask;

        if (context.User.CanSeeAllBranches || context.Resource is not HttpContext http)
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var requested = http.Request.RouteValues.TryGetValue("branch", out var routeValue)
            ? routeValue?.ToString()
            : http.Request.Query["branch"].ToString();

        if (string.IsNullOrWhiteSpace(requested)
            || string.Equals(requested.Trim(), home, StringComparison.OrdinalIgnoreCase))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
