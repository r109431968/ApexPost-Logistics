using ApexPost.Application.Exceptions;
using ApexPost.Web.Filters;
using ApexPost.Web.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

[Authorize(Policy = AuthPolicies.AdministratorOnly)]
[IpRestricted(IpZone.Admin)]
public sealed class StaffController(IStaffService staff) : ApexController
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(new StaffListViewModel { Staff = await staff.ListAsync(cancellationToken), CurrentStaffId = StaffId });

    [HttpGet]
    public IActionResult Create() => View(new StaffFormViewModel { HomeBranch = HomeBranch.Value });

    [HttpPost]
    [Audit("StaffCreate")]
    public async Task<IActionResult> Create(StaffFormViewModel model, CancellationToken cancellationToken)
    {
        var member = await staff.CreateAsync(ToRequest(model), model.Password ?? string.Empty, cancellationToken);

        AuditNotes.Add(HttpContext, "staffId", member.Id.ToString());
        Success($"{member.Name} was added.");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var member = await staff.GetAsync(id, cancellationToken);
        if (member is null)
            return NotFound();

        return View(new StaffFormViewModel
        {
            Id = member.Id,
            Name = member.Name,
            Email = member.Email,
            Role = member.Role,
            HomeBranch = member.HomeBranch.Value
        });
    }

    [HttpPost]
    [Audit("StaffUpdate")]
    public async Task<IActionResult> Edit(Guid id, StaffFormViewModel model, CancellationToken cancellationToken)
    {
        model.Id = id;
        await staff.UpdateAsync(id, ToRequest(model), cancellationToken);

        Success("Staff member updated.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Audit("StaffDeactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        await staff.SetActiveAsync(id, false, StaffId, cancellationToken);
        Success("Staff member deactivated.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Audit("StaffActivate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        await staff.SetActiveAsync(id, true, StaffId, cancellationToken);
        Success("Staff member activated.");
        return RedirectToAction(nameof(Index));
    }

    private static StaffRequest ToRequest(StaffFormViewModel model) => new()
    {
        Name = model.Name,
        Email = model.Email,
        Role = model.Role,
        HomeBranch = model.HomeBranch
    };
}
