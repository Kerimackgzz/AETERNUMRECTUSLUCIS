using Microsoft.AspNetCore.Http;

namespace AETKAHVE.Infrastructure.Options;

public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    public int AdminIdleTimeoutMinutes { get; set; } = 15;

    public int SuperAdminIdleTimeoutMinutes { get; set; } = 10;

    public int CustomerRememberMeDays { get; set; } = 30;

    public int AdminRememberMeHours { get; set; } = 12;

    public int SuperAdminRememberMeHours { get; set; } = 4;

    public int IdleWarningSeconds { get; set; } = 60;

    public string AdminRoute { get; set; } = "admin";

    public string SuperAdminRoute { get; set; } = "superadmin";

    public int MaxFailedAccessAttempts { get; set; } = 5;

    public int LockoutMinutes { get; set; } = 15;

    public int CustomerLoginRequestsPerMinute { get; set; } = 10;

    public int AdminLoginRequestsPerMinute { get; set; } = 5;

    public int SuperAdminLoginRequestsPerMinute { get; set; } = 5;

    public int CustomerRegistrationRequestsPerMinute { get; set; } = 5;

    public int PasswordRecoveryRequestsPerMinute { get; set; } = 5;

    public int RegistrationConfirmationTokenMinutes { get; set; } = 60;

    public int PendingRegistrationRetentionDays { get; set; } = 7;

    public int ContactRequestsPerMinute { get; set; } = 5;

    public CookieSecurePolicy CookieSecurePolicy { get; set; } = CookieSecurePolicy.SameAsRequest;

    public SameSiteMode CookieSameSiteMode { get; set; } = SameSiteMode.Lax;
}

