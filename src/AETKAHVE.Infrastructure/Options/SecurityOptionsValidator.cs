using Microsoft.Extensions.Options;

namespace AETKAHVE.Infrastructure.Options;

public sealed class SecurityOptionsValidator : IValidateOptions<SecurityOptions>
{
    public ValidateOptionsResult Validate(string? name, SecurityOptions options)
    {
        var failures = new List<string>();

        ValidateRange(options.AdminIdleTimeoutMinutes, 1, 120, nameof(options.AdminIdleTimeoutMinutes), failures);
        ValidateRange(options.SuperAdminIdleTimeoutMinutes, 1, 120, nameof(options.SuperAdminIdleTimeoutMinutes), failures);
        ValidateRange(options.CustomerRememberMeDays, 1, 365, nameof(options.CustomerRememberMeDays), failures);
        ValidateRange(options.AdminRememberMeHours, 1, 168, nameof(options.AdminRememberMeHours), failures);
        ValidateRange(options.SuperAdminRememberMeHours, 1, 72, nameof(options.SuperAdminRememberMeHours), failures);
        ValidateRange(options.IdleWarningSeconds, 10, 600, nameof(options.IdleWarningSeconds), failures);
        ValidateRange(options.MaxFailedAccessAttempts, 3, 20, nameof(options.MaxFailedAccessAttempts), failures);
        ValidateRange(options.LockoutMinutes, 1, 1440, nameof(options.LockoutMinutes), failures);
        ValidateRange(options.CustomerLoginRequestsPerMinute, 1, 1000, nameof(options.CustomerLoginRequestsPerMinute), failures);
        ValidateRange(options.AdminLoginRequestsPerMinute, 1, 1000, nameof(options.AdminLoginRequestsPerMinute), failures);
        ValidateRange(options.SuperAdminLoginRequestsPerMinute, 1, 1000, nameof(options.SuperAdminLoginRequestsPerMinute), failures);
        ValidateRange(options.CustomerRegistrationRequestsPerMinute, 1, 1000, nameof(options.CustomerRegistrationRequestsPerMinute), failures);
        ValidateRange(options.PasswordRecoveryRequestsPerMinute, 1, 1000, nameof(options.PasswordRecoveryRequestsPerMinute), failures);
        ValidateRange(options.ContactRequestsPerMinute, 1, 1000, nameof(options.ContactRequestsPerMinute), failures);

        ValidateRoute(options.AdminRoute, nameof(options.AdminRoute), failures);
        ValidateRoute(options.SuperAdminRoute, nameof(options.SuperAdminRoute), failures);
        ValidateFixedRoute(options.AdminRoute, "admin", nameof(options.AdminRoute), failures);
        ValidateFixedRoute(options.SuperAdminRoute, "superadmin", nameof(options.SuperAdminRoute), failures);

        if (string.Equals(options.AdminRoute, options.SuperAdminRoute, StringComparison.OrdinalIgnoreCase))
        {
            failures.Add("AdminRoute and SuperAdminRoute must be different.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateRange(int value, int minimum, int maximum, string name, ICollection<string> failures)
    {
        if (value < minimum || value > maximum)
        {
            failures.Add($"{name} must be between {minimum} and {maximum}.");
        }
    }

    private static void ValidateRoute(string value, string name, ICollection<string> failures)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Contains('/') || value.Contains('\\'))
        {
            failures.Add($"{name} must be a non-empty single URL segment.");
        }
    }

    private static void ValidateFixedRoute(
        string value,
        string expectedValue,
        string name,
        ICollection<string> failures)
    {
        if (!string.Equals(value, expectedValue, StringComparison.OrdinalIgnoreCase))
        {
            failures.Add($"{name} must be '{expectedValue}' because management endpoint routes are fixed by contract.");
        }
    }
}

