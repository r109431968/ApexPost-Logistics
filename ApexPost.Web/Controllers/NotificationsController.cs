using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

[Authorize(Policy = AuthPolicies.CanViewReports)]
public sealed class NotificationsController(INotificationSink sink) : ApexController
{
    public IActionResult Index() => View(sink.Recent(100));
}
