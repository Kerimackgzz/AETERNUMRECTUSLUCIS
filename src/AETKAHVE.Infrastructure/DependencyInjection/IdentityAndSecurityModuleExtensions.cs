using AETKAHVE.Application.Security;
using AETKAHVE.Infrastructure.Identity;
using AETKAHVE.Infrastructure.Options;
using AETKAHVE.Infrastructure.Persistence;
using AETKAHVE.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AETKAHVE.Infrastructure.DependencyInjection;

public static class SecurityRateLimitPolicies
{
    public const string CustomerLogin = "CustomerLogin";
    public const string AdminLogin = "AdminLogin";
    public const string SuperAdminLogin = "SuperAdminLogin";
}

public static class IdentityAndSecurityModuleExtensions
{
    public static IServiceCollection AddIdentityAndSecurityModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IValidateOptions<SecurityOptions>, SecurityOptionsValidator>();
        services.AddOptions<SecurityOptions>()
            .Bind(configuration.GetSection(SecurityOptions.SectionName))
            .ValidateOnStart();
        services.AddOptions<IdentitySeedOptions>()
            .Bind(configuration.GetSection(IdentitySeedOptions.SectionName));

        var security = configuration.GetSection(SecurityOptions.SectionName).Get<SecurityOptions>() ?? new();

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.SignIn.RequireConfirmedEmail = true;
                options.Password.RequiredLength = 12;
                options.Password.RequiredUniqueChars = 4;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = security.MaxFailedAccessAttempts;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(security.LockoutMinutes);
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = AuthenticationSchemes.Customer;
                options.DefaultChallengeScheme = AuthenticationSchemes.Customer;
                options.DefaultSignInScheme = AuthenticationSchemes.Customer;
            })
            .AddCookie(AuthenticationSchemes.Customer, options =>
            {
                ConfigureCookie(options, CookieNames.Customer, "/account/login", "/account/access-denied", security);
                options.ExpireTimeSpan = TimeSpan.FromDays(security.CustomerRememberMeDays);
                options.SlidingExpiration = true;
                options.EventsType = typeof(CustomerCookieEvents);
            })
            .AddCookie(AuthenticationSchemes.Admin, options =>
            {
                ConfigureCookie(options, CookieNames.Admin, $"/{security.AdminRoute}/login", $"/{security.AdminRoute}/access-denied", security);
                options.ExpireTimeSpan = TimeSpan.FromHours(security.AdminRememberMeHours);
                options.SlidingExpiration = false;
                options.EventsType = typeof(AdminCookieEvents);
            })
            .AddCookie(AuthenticationSchemes.SuperAdmin, options =>
            {
                ConfigureCookie(options, CookieNames.SuperAdmin, $"/{security.SuperAdminRoute}/login", $"/{security.SuperAdminRoute}/access-denied", security);
                options.ExpireTimeSpan = TimeSpan.FromHours(security.SuperAdminRememberMeHours);
                options.SlidingExpiration = false;
                options.EventsType = typeof(SuperAdminCookieEvents);
            })
            .AddPolicyScheme(AuthenticationSchemes.Management, AuthenticationSchemes.Management, options =>
            {
                options.ForwardDefaultSelector = context =>
                    context.Request.Cookies.ContainsKey(CookieNames.SuperAdmin)
                        ? AuthenticationSchemes.SuperAdmin
                        : AuthenticationSchemes.Admin;
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(AuthorizationPolicies.CustomerOnly, policy =>
            {
                policy.AddAuthenticationSchemes(AuthenticationSchemes.Customer);
                policy.RequireAuthenticatedUser();
                policy.RequireRole(RoleNames.Customer);
            })
            .AddPolicy(AuthorizationPolicies.AdminArea, policy =>
            {
                policy.AddAuthenticationSchemes(AuthenticationSchemes.Management);
                policy.RequireAuthenticatedUser();
                policy.RequireRole(RoleNames.Admin, RoleNames.SuperAdmin);
            })
            .AddPolicy(AuthorizationPolicies.SuperAdminArea, policy =>
            {
                policy.AddAuthenticationSchemes(AuthenticationSchemes.SuperAdmin);
                policy.RequireAuthenticatedUser();
                policy.RequireRole(RoleNames.SuperAdmin);
            });

        services.AddHttpContextAccessor();
        services.AddScoped<CustomerCookieEvents>();
        services.AddScoped<AdminCookieEvents>();
        services.AddScoped<SuperAdminCookieEvents>();
        services.AddScoped<SecurityAuditWriter>();
        services.AddScoped<ManagementSessionService>();
        services.AddScoped<AuthenticationSessionService>();
        services.AddScoped<IdentitySeeder>();
        services.AddHostedService<IdentitySeedHostedService>();
        return services;
    }

    private static void ConfigureCookie(
        CookieAuthenticationOptions options,
        string cookieName,
        string loginPath,
        string accessDeniedPath,
        SecurityOptions security)
    {
        options.Cookie.Name = cookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SecurePolicy = security.CookieSecurePolicy;
        options.Cookie.SameSite = security.CookieSameSiteMode;
        options.LoginPath = loginPath;
        options.AccessDeniedPath = accessDeniedPath;
    }

}
