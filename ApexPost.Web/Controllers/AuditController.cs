using ApexPost.Web.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

[Authorize(Policy = AuthPolicies.AuditViewers)]
[IpRestricted(IpZone.Admin)]
public sealed class AuditController(IAuditStore store) : ApexController
{
    public async Task<IActionResult> Index(int count = 100, CancellationToken cancellationToken = default) =>
        View(await store.ReadRecentAsync(Math.Clamp(count, 1, 500), cancellationToken));

    /// <summary>The same entries as JSON, for tools and for AUDIT-SAMPLE.json.</summary>
    [HttpGet]
    public async Task<IActionResult> Export(int count = 50, CancellationToken cancellationToken = default) =>
        Json(await store.ReadRecentAsync(Math.Clamp(count, 1, 500), cancellationToken));
}
