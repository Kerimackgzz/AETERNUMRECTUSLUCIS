using AETKAHVE.Application.Security;
using AETKAHVE.Infrastructure.Options;

namespace AETKAHVE.Infrastructure.Security;

public static class ManagementSessionPolicy
{
    public static TimeSpan IdleTimeout(SecurityOptions options, AuthenticationPortal portal) => portal switch
    {
        AuthenticationPortal.Admin => TimeSpan.FromMinutes(options.AdminIdleTimeoutMinutes),
        AuthenticationPortal.SuperAdmin => TimeSpan.FromMinutes(options.SuperAdminIdleTimeoutMinutes),
        _ => throw new ArgumentOutOfRangeException(nameof(portal)),
    };

    public static TimeSpan AbsoluteLifetime(SecurityOptions options, AuthenticationPortal portal) => portal switch
    {
        AuthenticationPortal.Admin => TimeSpan.FromHours(options.AdminRememberMeHours),
        AuthenticationPortal.SuperAdmin => TimeSpan.FromHours(options.SuperAdminRememberMeHours),
        _ => throw new ArgumentOutOfRangeException(nameof(portal)),
    };

    public static bool IsIdleExpired(ManagementSession session, SecurityOptions options, DateTimeOffset now) =>
        now - session.LastActivityAtUtc >= IdleTimeout(options, session.Portal);

    public static IdleSessionStatus CreateStatus(ManagementSession session, SecurityOptions options, DateTimeOffset now)
    {
        var idleExpiresAt = session.LastActivityAtUtc.Add(IdleTimeout(options, session.Portal));
        var effectiveExpiry = idleExpiresAt <= session.AbsoluteExpiresAtUtc
            ? idleExpiresAt
            : session.AbsoluteExpiresAtUtc;
        var remaining = Math.Max(0, (int)Math.Ceiling((effectiveExpiry - now).TotalSeconds));
        return new IdleSessionStatus(true, now, effectiveExpiry, remaining, options.IdleWarningSeconds);
    }
}

