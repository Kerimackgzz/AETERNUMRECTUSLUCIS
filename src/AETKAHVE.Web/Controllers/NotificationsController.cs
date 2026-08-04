using AETKAHVE.Application.Commerce;
using AETKAHVE.Application.Security;
using AETKAHVE.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AETKAHVE.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.CustomerOnly)]
[Route("account/notifications")]
public sealed class NotificationsController(INotificationService notificationService) : CommerceControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) => View(await notificationService.GetAsync(RequiredUserId, cancellationToken));

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        var result = await notificationService.MarkReadAsync(RequiredUserId, id, cancellationToken);
        return result.Succeeded ? Ok(new CommerceMutationResponse(true, result.Message)) : NotFound();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        var result = await notificationService.MarkReadAsync(RequiredUserId, null, cancellationToken);
        return Ok(new CommerceMutationResponse(true, result.Message));
    }
}
