using System.Globalization;
using System.Threading.RateLimiting;
using AETKAHVE.Infrastructure.DependencyInjection;
using AETKAHVE.Infrastructure.Options;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace AETKAHVE.Web.DependencyInjection;

public static class WebSecurityModuleExtensions
{
    public static IServiceCollection AddWebSecurityModule(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds)
                        .ToString(CultureInfo.InvariantCulture);
                }

                return ValueTask.CompletedTask;
            };
            AddIpPolicy(options, SecurityRateLimitPolicies.CustomerLogin, security => security.CustomerLoginRequestsPerMinute);
            AddIpPolicy(options, SecurityRateLimitPolicies.AdminLogin, security => security.AdminLoginRequestsPerMinute);
            AddIpPolicy(options, SecurityRateLimitPolicies.SuperAdminLogin, security => security.SuperAdminLoginRequestsPerMinute);
            AddIpPolicy(options, SecurityRateLimitPolicies.CustomerRegistration, security => security.CustomerRegistrationRequestsPerMinute);
            AddIpPolicy(options, SecurityRateLimitPolicies.PasswordRecovery, security => security.PasswordRecoveryRequestsPerMinute);
        });
        return services;
    }

    private static void AddIpPolicy(
        RateLimiterOptions options,
        string policyName,
        Func<SecurityOptions, int> permitLimit)
    {
        options.AddPolicy(policyName, context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit(context.RequestServices.GetRequiredService<IOptions<SecurityOptions>>().Value),
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));
    }
}

