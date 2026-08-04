using AETKAHVE.Application.Security;
using AETKAHVE.Infrastructure.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AETKAHVE.Infrastructure.Identity;

public sealed class IdentitySeeder(
    RoleManager<ApplicationRole> roleManager,
    UserManager<ApplicationUser> userManager,
    IOptions<IdentitySeedOptions> seedOptions,
    TimeProvider timeProvider)
{
    private readonly IdentitySeedOptions _seedOptions = seedOptions.Value;

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        foreach (var roleName in RoleNames.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var roleResult = await roleManager.CreateAsync(new ApplicationRole { Name = roleName });
                EnsureSucceeded(roleResult, $"create role {roleName}");
            }
        }

        await CreateManagementUserIfConfiguredAsync(
            _seedOptions.AdminEmail,
            _seedOptions.AdminPassword,
            RoleNames.Admin,
            cancellationToken);
        await CreateManagementUserIfConfiguredAsync(
            _seedOptions.SuperAdminEmail,
            _seedOptions.SuperAdminPassword,
            RoleNames.SuperAdmin,
            cancellationToken);
    }

    private async Task CreateManagementUserIfConfiguredAsync(
        string? email,
        string? password,
        string role,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = email.Trim(),
                Email = email.Trim(),
                EmailConfirmed = true,
                FirstName = "Development",
                LastName = role,
                CreatedAtUtc = timeProvider.GetUtcNow(),
                IsActive = true,
            };
            EnsureSucceeded(await userManager.CreateAsync(user, password), $"create {role} user");
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            EnsureSucceeded(await userManager.AddToRoleAsync(user, role), $"assign {role} role");
        }
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (!result.Succeeded)
        {
            var errorCodes = string.Join(", ", result.Errors.Select(x => x.Code));
            throw new InvalidOperationException($"Identity seed could not {operation}: {errorCodes}");
        }
    }
}

public sealed class IdentitySeedHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<IdentitySeedOptions> seedOptions) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!seedOptions.Value.Enabled)
        {
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IdentitySeeder>().SeedAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
