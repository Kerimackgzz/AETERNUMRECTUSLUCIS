using AETKAHVE.Application.Security;
using AETKAHVE.Infrastructure.Identity;
using AETKAHVE.Infrastructure.Options;
using AETKAHVE.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AETKAHVE.IntegrationTests;

public sealed class IdentitySeederTests(AeternumWebApplicationFactory factory)
    : IClassFixture<AeternumWebApplicationFactory>
{
    [Fact]
    public async Task Existing_single_superadmin_can_receive_admin_role_idempotently()
    {
        var email = AeternumWebApplicationFactory.SuperAdminEmail;
        var options = DualRoleOptions(email);

        await SeedAsync(options);
        await SeedAsync(options);

        await using var scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = Assert.IsType<ApplicationUser>(await userManager.FindByEmailAsync(email));
        var roles = await userManager.GetRolesAsync(user);
        Assert.Contains(RoleNames.Admin, roles);
        Assert.Contains(RoleNames.SuperAdmin, roles);
        Assert.Equal(1, userManager.Users.Count(item => item.NormalizedEmail == user.NormalizedEmail));
    }

    [Fact]
    public async Task Configured_admin_remains_admin_without_becoming_superadmin()
    {
        var email = $"existing-admin-{Guid.NewGuid():N}@test.local";
        await CreateUserAsync(email, RoleNames.Admin);

        await SeedAsync(SeparatedRoleOptions(email));

        await using var scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = Assert.IsType<ApplicationUser>(await userManager.FindByEmailAsync(email));
        Assert.True(await userManager.IsInRoleAsync(user, RoleNames.Admin));
        Assert.False(await userManager.IsInRoleAsync(user, RoleNames.SuperAdmin));
        Assert.Single(await userManager.GetUsersInRoleAsync(RoleNames.SuperAdmin));
    }

    [Fact]
    public async Task Seeder_refuses_to_create_a_second_superadmin()
    {
        var secondEmail = $"second-superadmin-seed-{Guid.NewGuid():N}@test.local";

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SeedAsync(DualRoleOptions(secondEmail)));

        Assert.Contains("second SuperAdmin", exception.Message, StringComparison.Ordinal);
        await using var scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Null(await userManager.FindByEmailAsync(secondEmail));
        Assert.Single(await userManager.GetUsersInRoleAsync(RoleNames.SuperAdmin));
    }

    [Fact]
    public async Task Existing_customer_only_account_is_never_elevated()
    {
        var email = $"customer-seed-guard-{Guid.NewGuid():N}@test.local";
        await CreateUserAsync(email, RoleNames.Customer);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SeedAsync(SeparatedRoleOptions(email)));

        Assert.Contains("non-management", exception.Message, StringComparison.Ordinal);
        await using var scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = Assert.IsType<ApplicationUser>(await userManager.FindByEmailAsync(email));
        Assert.True(await userManager.IsInRoleAsync(user, RoleNames.Customer));
        Assert.False(await userManager.IsInRoleAsync(user, RoleNames.Admin));
        Assert.False(await userManager.IsInRoleAsync(user, RoleNames.SuperAdmin));
    }

    [Fact]
    public async Task Retirement_runs_after_replacement_is_ready_and_is_idempotent()
    {
        var legacyEmail = $"legacy-management-{Guid.NewGuid():N}@test.local";
        var replacementEmail = $"replacement-{Guid.NewGuid():N}@test.local";
        await CreateUserAsync(legacyEmail, RoleNames.Admin);
        var options = SeparatedRoleOptions(replacementEmail);
        options.AllowDestructiveRetirement = true;
        options.RetireManagementEmails = [legacyEmail];

        await SeedAsync(options);
        await SeedAsync(options);

        await using var scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Null(await userManager.FindByEmailAsync(legacyEmail));
        var replacement = Assert.IsType<ApplicationUser>(await userManager.FindByEmailAsync(replacementEmail));
        Assert.True(await userManager.IsInRoleAsync(replacement, RoleNames.Admin));
        Assert.False(await userManager.IsInRoleAsync(replacement, RoleNames.SuperAdmin));
        Assert.Single(await userManager.GetUsersInRoleAsync(RoleNames.SuperAdmin));
    }

    [Fact]
    public async Task Failed_replacement_creation_does_not_retire_existing_management_user()
    {
        var legacyEmail = $"preserved-management-{Guid.NewGuid():N}@test.local";
        await CreateUserAsync(legacyEmail, RoleNames.Admin);
        var options = SeparatedRoleOptions($"invalid-replacement-{Guid.NewGuid():N}@test.local");
        options.AdminPassword = "short";
        options.AllowDestructiveRetirement = true;
        options.RetireManagementEmails = [legacyEmail];

        await Assert.ThrowsAsync<InvalidOperationException>(() => SeedAsync(options));

        await using var scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.NotNull(await userManager.FindByEmailAsync(legacyEmail));
    }

    [Fact]
    public async Task Retirement_refuses_accounts_that_have_customer_role()
    {
        var customerEmail = $"retirement-customer-{Guid.NewGuid():N}@test.local";
        await CreateUserAsync(customerEmail, RoleNames.Customer);
        var options = SeparatedRoleOptions($"safe-replacement-{Guid.NewGuid():N}@test.local");
        options.AllowDestructiveRetirement = true;
        options.RetireManagementEmails = [customerEmail];

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => SeedAsync(options));

        Assert.Contains("management-only", exception.Message, StringComparison.Ordinal);
        await using var scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.NotNull(await userManager.FindByEmailAsync(customerEmail));
    }

    [Fact]
    public async Task Retirement_validates_every_target_before_deleting_any_target()
    {
        var managementEmail = $"retirement-preserved-{Guid.NewGuid():N}@test.local";
        var customerEmail = $"retirement-invalid-{Guid.NewGuid():N}@test.local";
        await CreateUserAsync(managementEmail, RoleNames.Admin);
        await CreateUserAsync(customerEmail, RoleNames.Customer);
        var options = SeparatedRoleOptions($"ordered-replacement-{Guid.NewGuid():N}@test.local");
        options.AllowDestructiveRetirement = true;
        options.RetireManagementEmails = [managementEmail, customerEmail];

        await Assert.ThrowsAsync<InvalidOperationException>(() => SeedAsync(options));

        await using var scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.NotNull(await userManager.FindByEmailAsync(managementEmail));
        Assert.NotNull(await userManager.FindByEmailAsync(customerEmail));
    }

    private async Task SeedAsync(IdentitySeedOptions options)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var seeder = new IdentitySeeder(
            scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>(),
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
            Options.Create(options),
            factory.Clock);
        await seeder.SeedAsync();
    }

    private async Task CreateUserAsync(string email, string role)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = "Seed",
            LastName = "Guard",
            CreatedAtUtc = factory.Clock.GetUtcNow(),
            IsActive = true,
        };
        Assert.True((await userManager.CreateAsync(user, AeternumWebApplicationFactory.Password)).Succeeded);
        Assert.True((await userManager.AddToRoleAsync(user, role)).Succeeded);
    }

    private static IdentitySeedOptions DualRoleOptions(string email) => new()
    {
        Enabled = true,
        AdminEmail = email,
        AdminPassword = AeternumWebApplicationFactory.Password,
        SuperAdminEmail = email.ToUpperInvariant(),
        SuperAdminPassword = AeternumWebApplicationFactory.Password,
    };

    private static IdentitySeedOptions SeparatedRoleOptions(string adminEmail) => new()
    {
        Enabled = true,
        AdminEmail = adminEmail,
        AdminPassword = AeternumWebApplicationFactory.Password,
        SuperAdminEmail = AeternumWebApplicationFactory.SuperAdminEmail,
        SuperAdminPassword = AeternumWebApplicationFactory.Password,
    };
}
