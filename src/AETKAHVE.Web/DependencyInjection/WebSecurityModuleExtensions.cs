using System.Threading.RateLimiting;
using AETKAHVE.Infrastructure.DependencyInjection;
using Microsoft.AspNetCore.RateLimiting;

namespace AETKAHVE.Web.DependencyInjection;

public static class WebSecurityModuleExtensions
{
    public static IServiceCollection AddWebSecurityModule(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            AddIpPolicy(options, SecurityRateLimitPolicies.CustomerLogin, permitLimit: 10);
            AddIpPolicy(options, SecurityRateLimitPolicies.AdminLogin, permitLimit: 5);
            AddIpPolicy(options, SecurityRateLimitPolicies.SuperAdminLogin, permitLimit: 5);
        });
        return services;
    }

    private static void AddIpPolicy(RateLimiterOptions options, string policyName, int permitLimit)
    {
        options.AddPolicy(policyName, context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));
    }
}

