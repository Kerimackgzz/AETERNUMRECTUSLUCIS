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
        var seedPlans = BuildSeedPlans();
        var retirementEmails = BuildRetirementPlan(seedPlans);

        foreach (var roleName in RoleNames.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var roleResult = await roleManager.CreateAsync(new ApplicationRole { Name = roleName });
                EnsureSucceeded(roleResult, $"create role {roleName}");
            }
        }

        await EnsureSingleSuperAdminBeforeSeedAsync(seedPlans);

        foreach (var plan in seedPlans)
        {
            await CreateOrCompleteManagementUserAsync(plan, cancellationToken);
        }

        await RetireManagementUsersAsync(retirementEmails, cancellationToken);
        await EnsureAtMostOneSuperAdminAsync();
    }

    private async Task EnsureSingleSuperAdminBeforeSeedAsync(IReadOnlyList<ManagementSeedPlan> seedPlans)
    {
        var existing = await userManager.GetUsersInRoleAsync(RoleNames.SuperAdmin);
        if (existing.Count > 1)
        {
            throw new InvalidOperationException(
                "Identity seed refused to continue because more than one SuperAdmin exists.");
        }

        var configured = seedPlans.SingleOrDefault(plan =>
            plan.Roles.Contains(RoleNames.SuperAdmin, StringComparer.OrdinalIgnoreCase));
        if (existing.Count == 1
            && configured is not null
            && !string.Equals(
                IdentitySeedOptionsValidator.NormalizeEmail(existing[0].Email ?? string.Empty),
                IdentitySeedOptionsValidator.NormalizeEmail(configured.Email),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Identity seed refused to create a second SuperAdmin. SuperAdmin replacement requires a separate controlled maintenance operation.");
        }
    }

    private async Task EnsureAtMostOneSuperAdminAsync()
    {
        var superAdmins = await userManager.GetUsersInRoleAsync(RoleNames.SuperAdmin);
        if (superAdmins.Count > 1)
        {
            throw new InvalidOperationException("The system cannot contain more than one SuperAdmin.");
        }
    }

    private async Task CreateOrCompleteManagementUserAsync(
        ManagementSeedPlan plan,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await userManager.FindByEmailAsync(plan.Email);
        var createdBySeeder = user is null;
        if (user is null)
        {
            user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = plan.Email,
                Email = plan.Email,
                EmailConfirmed = true,
                FirstName = "Management",
                LastName = "Account",
                CreatedAtUtc = timeProvider.GetUtcNow(),
                IsActive = true,
            };
            EnsureSucceeded(await userManager.CreateAsync(user, plan.Password), "create management user");
        }

        var existingRoles = await userManager.GetRolesAsync(user);
        if (!createdBySeeder
            && !existingRoles.Any(IsManagementRole))
        {
            throw new InvalidOperationException(
                "Identity seed refused to grant management access to an existing non-management account.");
        }

        foreach (var role in plan.Roles)
        {
            if (!existingRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
            {
                EnsureSucceeded(await userManager.AddToRoleAsync(user, role), $"assign {role} role");
            }
        }
    }

    private IReadOnlyList<ManagementSeedPlan> BuildSeedPlans()
    {
        var configured = new[]
        {
            new SeedEntry(_seedOptions.AdminEmail, _seedOptions.AdminPassword, RoleNames.Admin),
            new SeedEntry(_seedOptions.SuperAdminEmail, _seedOptions.SuperAdminPassword, RoleNames.SuperAdmin),
        };

        foreach (var entry in configured)
        {
            if (string.IsNullOrWhiteSpace(entry.Email) != string.IsNullOrWhiteSpace(entry.Password))
            {
                throw new InvalidOperationException(
                    $"Identity seed requires both the {entry.Role} email and password configuration values.");
            }
        }

        var plans = new List<ManagementSeedPlan>();
        foreach (var group in configured
                     .Where(entry => IdentitySeedOptionsValidator.HasCompletePair(entry.Email, entry.Password))
                     .GroupBy(
                         entry => IdentitySeedOptionsValidator.NormalizeEmail(entry.Email!),
                         StringComparer.Ordinal))
        {
            var entries = group.ToArray();
            if (entries.Select(entry => entry.Password).Distinct(StringComparer.Ordinal).Count() != 1)
            {
                throw new InvalidOperationException(
                    "Identity seed passwords must match when management roles use the same email address.");
            }

            plans.Add(new ManagementSeedPlan(
                entries[0].Email!.Trim(),
                entries[0].Password!,
                entries.Select(entry => entry.Role).Distinct(StringComparer.Ordinal).ToArray()));
        }

        return plans;
    }

    private IReadOnlyList<string> BuildRetirementPlan(IReadOnlyList<ManagementSeedPlan> seedPlans)
    {
        var retirementEmails = _seedOptions.RetireManagementEmails
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Select(email => email.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (retirementEmails.Length == 0)
        {
            return retirementEmails;
        }

        if (!_seedOptions.AllowDestructiveRetirement)
        {
            throw new InvalidOperationException(
                "Identity seed refused to retire management users without explicit destructive retirement approval.");
        }

        var replacementEmails = seedPlans
            .Select(plan => IdentitySeedOptionsValidator.NormalizeEmail(plan.Email))
            .ToHashSet(StringComparer.Ordinal);
        var replacementRoles = seedPlans
            .SelectMany(plan => plan.Roles)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!replacementRoles.Contains(RoleNames.Admin)
            || !replacementRoles.Contains(RoleNames.SuperAdmin))
        {
            throw new InvalidOperationException(
                "Identity seed requires configured Admin and SuperAdmin replacement roles before retiring management users.");
        }

        foreach (var email in retirementEmails)
        {
            if (replacementEmails.Contains(IdentitySeedOptionsValidator.NormalizeEmail(email)))
            {
                throw new InvalidOperationException(
                    "Identity seed refused to retire a configured replacement management account.");
            }
        }

        return retirementEmails;
    }

    private async Task RetireManagementUsersAsync(
        IReadOnlyList<string> retirementEmails,
        CancellationToken cancellationToken)
    {
        var usersToRetire = new List<ApplicationUser>();
        foreach (var email in retirementEmails)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
            {
                continue;
            }

            var roles = await userManager.GetRolesAsync(user);
            if (roles.Count == 0 || roles.Any(role => !IsManagementRole(role)))
            {
                throw new InvalidOperationException(
                    "Identity seed refused to retire an account that is not management-only.");
            }

            usersToRetire.Add(user);
        }

        foreach (var user in usersToRetire)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureSucceeded(await userManager.DeleteAsync(user), "retire management user");
        }
    }

    private static bool IsManagementRole(string role) =>
        string.Equals(role, RoleNames.Admin, StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, RoleNames.SuperAdmin, StringComparison.OrdinalIgnoreCase);

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (!result.Succeeded)
        {
            var errorCodes = string.Join(", ", result.Errors.Select(x => x.Code));
            throw new InvalidOperationException($"Identity seed could not {operation}: {errorCodes}");
        }
    }

    private sealed record SeedEntry(string? Email, string? Password, string Role);

    private sealed record ManagementSeedPlan(string Email, string Password, IReadOnlyList<string> Roles);
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

public sealed class SingleSuperAdminInvariantHostedService(
    IServiceScopeFactory scopeFactory,
    IHostEnvironment environment) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Integration fixtures create their isolated schema and users after the host starts.
        // The invariant is exercised there through IdentitySeeder directly.
        if (environment.IsEnvironment("Testing"))
        {
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        if (!await roleManager.RoleExistsAsync(RoleNames.SuperAdmin))
        {
            if (environment.IsProduction())
            {
                throw new InvalidOperationException(
                    "Production requires exactly one active SuperAdmin, but the SuperAdmin role does not exist.");
            }

            return;
        }

        var superAdmins = await userManager.GetUsersInRoleAsync(RoleNames.SuperAdmin);
        if (superAdmins.Count > 1)
        {
            throw new InvalidOperationException(
                "The system cannot start because more than one SuperAdmin exists.");
        }

        if (environment.IsProduction()
            && (superAdmins.Count != 1
                || !superAdmins[0].IsActive
                || superAdmins[0].DeletedAtUtc.HasValue))
        {
            throw new InvalidOperationException(
                "Production requires exactly one active, non-deleted SuperAdmin.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
