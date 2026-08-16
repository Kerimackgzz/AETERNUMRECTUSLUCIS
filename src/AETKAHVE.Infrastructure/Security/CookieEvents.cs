using System.Security.Claims;
using AETKAHVE.Application.Security;
using AETKAHVE.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

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
            await context.HttpContext.SignOutAsync(context.Scheme.Name);
        }
    }
}

public abstract class ManagementCookieEvents(
    UserManager<ApplicationUser> userManager,
    ManagementSessionService managementSessions,
    SecurityAuditWriter auditWriter,
    AuthenticationPortal portal) : CookieAuthenticationEvents
{
    private const string LoginReasonItemKey = "AETKAHVE.ManagementLoginReason";

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context) =>
        RedirectOrStatusCode(context, StatusCodes.Status401Unauthorized, GetLoginReason(context.HttpContext));

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context) =>
        RedirectOrStatusCode(context, StatusCodes.Status403Forbidden);

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var userIdValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var sessionIdValue = context.Principal?.FindFirstValue(SecurityClaimTypes.SessionId);
        var securityStamp = context.Principal?.FindFirstValue(SecurityClaimTypes.SecurityStamp);

        if (!Guid.TryParse(userIdValue, out var userId) || !Guid.TryParse(sessionIdValue, out var sessionId))
        {
            await RejectAndDeleteCookieAsync(context, "session-ended");
            return;
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        var expectedRole = portal == AuthenticationPortal.Admin ? RoleNames.Admin : RoleNames.SuperAdmin;
        if (user is null || !user.IsActive || user.DeletedAtUtc.HasValue ||
            !await userManager.IsInRoleAsync(user, expectedRole))
        {
            await RejectAndDeleteCookieAsync(context, "session-ended");
            return;
        }

        if (!string.Equals(user.SecurityStamp, securityStamp, StringComparison.Ordinal))
        {
            await RejectAndDeleteCookieAsync(context, "credentials-changed");
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

        var loginReason = validation.IsIdleExpired
            ? "expired"
            : string.Equals(validation.Session?.RevocationReason, "EmailChanged", StringComparison.Ordinal) ||
              string.Equals(validation.Session?.RevocationReason, "PasswordChanged", StringComparison.Ordinal) ||
              string.Equals(validation.Session?.RevocationReason, "CredentialsChanged", StringComparison.Ordinal)
                ? "credentials-changed"
                : "session-ended";
        await RejectAndDeleteCookieAsync(context, loginReason);
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

    private static async Task RejectAndDeleteCookieAsync(
        CookieValidatePrincipalContext context,
        string loginReason)
    {
        context.HttpContext.Items[LoginReasonItemKey] = loginReason;
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(context.Scheme.Name);
    }

    private static Task RedirectOrStatusCode(
        RedirectContext<CookieAuthenticationOptions> context,
        int statusCode,
        string? loginReason = null)
    {
        if (context.Request.Path.Value?.Contains("/session/", StringComparison.OrdinalIgnoreCase) == true)
        {
            context.Response.StatusCode = statusCode;
        }
        else
        {
            var redirectUri = string.IsNullOrEmpty(loginReason)
                ? context.RedirectUri
                : QueryHelpers.AddQueryString(context.RedirectUri, "reason", loginReason);
            context.Response.Redirect(redirectUri);
        }

        return Task.CompletedTask;
    }

    private static string? GetLoginReason(HttpContext httpContext) =>
        httpContext.Items.TryGetValue(LoginReasonItemKey, out var value) ? value as string : null;
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

