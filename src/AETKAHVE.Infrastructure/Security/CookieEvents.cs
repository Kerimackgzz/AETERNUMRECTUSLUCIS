using System.Security.Claims;
using AETKAHVE.Application.Security;
using AETKAHVE.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

namespace AETKAHVE.Infrastructure.Security;

public sealed class CustomerCookieEvents(UserManager<ApplicationUser> userManager) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var securityStamp = context.Principal?.FindFirstValue(SecurityClaimTypes.SecurityStamp);
        var user = Guid.TryParse(userId, out var parsedUserId)
            ? await userManager.FindByIdAsync(parsedUserId.ToString())
            : null;

        if (user is null || !user.IsActive || user.DeletedAtUtc.HasValue ||
            !string.Equals(user.SecurityStamp, securityStamp, StringComparison.Ordinal) ||
            !await userManager.IsInRoleAsync(user, RoleNames.Customer))
        {
            context.RejectPrincipal();
        }
    }
}

public abstract class ManagementCookieEvents(
    UserManager<ApplicationUser> userManager,
    ManagementSessionService managementSessions,
    SecurityAuditWriter auditWriter,
    AuthenticationPortal portal) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var userIdValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var sessionIdValue = context.Principal?.FindFirstValue(SecurityClaimTypes.SessionId);
        var securityStamp = context.Principal?.FindFirstValue(SecurityClaimTypes.SecurityStamp);

        if (!Guid.TryParse(userIdValue, out var userId) || !Guid.TryParse(sessionIdValue, out var sessionId))
        {
            context.RejectPrincipal();
            return;
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        var expectedRole = portal == AuthenticationPortal.Admin ? RoleNames.Admin : RoleNames.SuperAdmin;
        if (user is null || !user.IsActive || user.DeletedAtUtc.HasValue ||
            !string.Equals(user.SecurityStamp, securityStamp, StringComparison.Ordinal) ||
            !await userManager.IsInRoleAsync(user, expectedRole))
        {
            context.RejectPrincipal();
            return;
        }

        var isStatusRequest = context.HttpContext.Request.Path.Value?.EndsWith(
            "/session/status",
            StringComparison.OrdinalIgnoreCase) == true;
        var validation = await managementSessions.ValidateAsync(
            sessionId,
            user,
            portal,
            touchActivity: !isStatusRequest,
            context.HttpContext.RequestAborted);

        if (validation.IsValid)
        {
            return;
        }

        context.RejectPrincipal();
        if (validation.IsIdleExpired)
        {
            await auditWriter.WriteAsync(
                "IdleTimeoutLogout",
                "Management session ended because its idle timeout elapsed.",
                user.Id,
                context.HttpContext.Connection.RemoteIpAddress?.ToString(),
                context.HttpContext.Request.Headers.UserAgent.ToString(),
                context.HttpContext.Request.Path,
                context.HttpContext.TraceIdentifier,
                context.HttpContext.RequestAborted);
        }
    }
}

public sealed class AdminCookieEvents(
    UserManager<ApplicationUser> userManager,
    ManagementSessionService managementSessions,
    SecurityAuditWriter auditWriter)
    : ManagementCookieEvents(userManager, managementSessions, auditWriter, AuthenticationPortal.Admin);

public sealed class SuperAdminCookieEvents(
    UserManager<ApplicationUser> userManager,
    ManagementSessionService managementSessions,
    SecurityAuditWriter auditWriter)
    : ManagementCookieEvents(userManager, managementSessions, auditWriter, AuthenticationPortal.SuperAdmin);

