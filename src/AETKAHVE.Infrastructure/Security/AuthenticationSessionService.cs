using System.Security.Claims;
using AETKAHVE.Application.Security;
using AETKAHVE.Infrastructure.Identity;
using AETKAHVE.Infrastructure.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace AETKAHVE.Infrastructure.Security;

public sealed class AuthenticationSessionService(
    UserManager<ApplicationUser> userManager,
    IUserClaimsPrincipalFactory<ApplicationUser> claimsPrincipalFactory,
    ManagementSessionService managementSessions,
    SecurityAuditWriter auditWriter,
    IOptions<SecurityOptions> securityOptions,
    TimeProvider timeProvider)
{
    private readonly SecurityOptions _securityOptions = securityOptions.Value;

    public async Task<SignInOutcome> PasswordSignInAsync(
        HttpContext httpContext,
        SignInAttempt attempt,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(attempt.Email.Trim());
        var requiredRole = RoleFor(attempt.Portal);

        if (user is null || !user.IsActive || user.DeletedAtUtc.HasValue ||
            !await userManager.IsInRoleAsync(user, requiredRole))
        {
            await AuditAsync(null, attempt, "LoginFailed", "Authentication failed.", cancellationToken);
            return new SignInOutcome(SignInStatus.Failed);
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            await AuditAsync(user.Id, attempt, "LoginLockedOut", "Authentication rejected for a locked account.", cancellationToken);
            return new SignInOutcome(SignInStatus.LockedOut);
        }

        if (!await userManager.CheckPasswordAsync(user, attempt.Password))
        {
            await userManager.AccessFailedAsync(user);
            await AuditAsync(user.Id, attempt, "LoginFailed", "Authentication failed.", cancellationToken);
            return new SignInOutcome(await userManager.IsLockedOutAsync(user) ? SignInStatus.LockedOut : SignInStatus.Failed);
        }

        if (attempt.Portal == AuthenticationPortal.Customer && !user.EmailConfirmed)
        {
            await AuditAsync(user.Id, attempt, "LoginFailed", "Authentication failed for an unavailable account.", cancellationToken);
            return new SignInOutcome(SignInStatus.Failed);
        }

        await userManager.ResetAccessFailedCountAsync(user);
        user.LastLoginAtUtc = timeProvider.GetUtcNow();
        await userManager.UpdateAsync(user);

        var principal = await claimsPrincipalFactory.CreateAsync(user);
        var identity = principal.Identities.First();
        identity.AddClaim(new Claim(SecurityClaimTypes.SecurityStamp, user.SecurityStamp ?? string.Empty));
        identity.AddClaim(new Claim(SecurityClaimTypes.Portal, attempt.Portal.ToString()));

        ManagementSession? managementSession = null;
        if (attempt.Portal != AuthenticationPortal.Customer)
        {
            managementSession = await managementSessions.CreateAsync(user, attempt.Portal, cancellationToken);
            identity.AddClaim(new Claim(SecurityClaimTypes.SessionId, managementSession.Id.ToString("D")));
        }

        var now = timeProvider.GetUtcNow();
        var lifetime = LifetimeFor(attempt.Portal);
        var properties = new AuthenticationProperties
        {
            IsPersistent = attempt.RememberMe,
            AllowRefresh = false,
            IssuedUtc = now,
            ExpiresUtc = managementSession?.AbsoluteExpiresAtUtc ?? now.Add(lifetime),
        };

        await httpContext.SignInAsync(SchemeFor(attempt.Portal), principal, properties);
        await AuditAsync(user.Id, attempt, "LoginSucceeded", "Authentication succeeded.", cancellationToken);
        return new SignInOutcome(SignInStatus.Succeeded);
    }

    public async Task SignOutAsync(
        HttpContext httpContext,
        AuthenticationPortal portal,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var sessionClaim = httpContext.User.FindFirstValue(SecurityClaimTypes.SessionId);
        if (Guid.TryParse(sessionClaim, out var sessionId))
        {
            await managementSessions.RevokeAsync(sessionId, reason, cancellationToken);
        }

        await httpContext.SignOutAsync(SchemeFor(portal));
    }

    public async Task SignOutAllManagementAsync(
        HttpContext httpContext,
        Guid userId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        await managementSessions.RevokeAllAsync(userId, reason, cancellationToken);
        await httpContext.SignOutAsync(AuthenticationSchemes.Admin);
        await httpContext.SignOutAsync(AuthenticationSchemes.SuperAdmin);
    }

    public static string SchemeFor(AuthenticationPortal portal) => portal switch
    {
        AuthenticationPortal.Customer => AuthenticationSchemes.Customer,
        AuthenticationPortal.Admin => AuthenticationSchemes.Admin,
        AuthenticationPortal.SuperAdmin => AuthenticationSchemes.SuperAdmin,
        _ => throw new ArgumentOutOfRangeException(nameof(portal)),
    };

    private static string RoleFor(AuthenticationPortal portal) => portal switch
    {
        AuthenticationPortal.Customer => RoleNames.Customer,
        AuthenticationPortal.Admin => RoleNames.Admin,
        AuthenticationPortal.SuperAdmin => RoleNames.SuperAdmin,
        _ => throw new ArgumentOutOfRangeException(nameof(portal)),
    };

    private TimeSpan LifetimeFor(AuthenticationPortal portal) => portal switch
    {
        AuthenticationPortal.Customer => TimeSpan.FromDays(_securityOptions.CustomerRememberMeDays),
        AuthenticationPortal.Admin => TimeSpan.FromHours(_securityOptions.AdminRememberMeHours),
        AuthenticationPortal.SuperAdmin => TimeSpan.FromHours(_securityOptions.SuperAdminRememberMeHours),
        _ => throw new ArgumentOutOfRangeException(nameof(portal)),
    };

    private Task AuditAsync(
        Guid? userId,
        SignInAttempt attempt,
        string action,
        string description,
        CancellationToken cancellationToken) =>
        auditWriter.WriteAsync(
            action,
            description,
            userId,
            attempt.IpAddress,
            attempt.UserAgent,
            attempt.Route,
            attempt.CorrelationId,
            cancellationToken);
}

