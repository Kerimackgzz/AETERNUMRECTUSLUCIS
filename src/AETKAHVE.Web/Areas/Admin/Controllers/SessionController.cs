using System.Security.Claims;
using AETKAHVE.Application.Security;
using AETKAHVE.Infrastructure.Identity;
using AETKAHVE.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AETKAHVE.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AuthorizationPolicies.AdminArea)]
[Route("admin/session")]
public sealed class SessionController(
    UserManager<ApplicationUser> userManager,
    ManagementSessionService managementSessions) : Controller
{
    [HttpGet("status")]
    public Task<IActionResult> Status(CancellationToken cancellationToken) => GetStatusAsync(false, cancellationToken);

    [HttpPost("keep-alive")]
    public Task<IActionResult> KeepAlive(CancellationToken cancellationToken) => GetStatusAsync(true, cancellationToken);

    private async Task<IActionResult> GetStatusAsync(bool touchActivity, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ||
            !Guid.TryParse(User.FindFirstValue(SecurityClaimTypes.SessionId), out var sessionId))
        {
            return Unauthorized();
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Unauthorized();
        }

        var validation = await managementSessions.ValidateAsync(
            sessionId,
            user,
            AuthenticationPortal.Admin,
            touchActivity,
            cancellationToken);
        return validation.IsValid && validation.Session is not null
            ? Json(managementSessions.ToStatus(validation.Session))
            : Unauthorized();
    }
}

