using AETKAHVE.Application.Security;
using AETKAHVE.Infrastructure.Identity;
using AETKAHVE.Infrastructure.Persistence;
using AETKAHVE.Infrastructure.Security;
using AETKAHVE.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AETKAHVE.IntegrationTests;

public sealed class LockoutTests(AeternumWebApplicationFactory factory)
    : IClassFixture<AeternumWebApplicationFactory>
{
    [Fact]
    public async Task Five_failed_management_attempts_lock_the_account_and_are_audited()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var authentication = scope.ServiceProvider.GetRequiredService<AuthenticationSessionService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };

        for (var attemptNumber = 0; attemptNumber < 5; attemptNumber++)
        {
            await authentication.PasswordSignInAsync(
                context,
                new SignInAttempt(
                    AeternumWebApplicationFactory.LockoutEmail,
                    "IncorrectPassword1!",
                    false,
                    AuthenticationPortal.Admin,
                    "127.0.0.1",
                    "integration-test",
                    "/admin/login",
                    Guid.NewGuid().ToString("N")));
        }

        var user = await userManager.FindByEmailAsync(AeternumWebApplicationFactory.LockoutEmail);
        Assert.True(await userManager.IsLockedOutAsync(Assert.IsType<ApplicationUser>(user)));
        var failures = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditLogs
            .CountAsync(log => log.ActionType == "LoginFailed" && log.AdminUserId == user!.Id);
        Assert.Equal(5, failures);
    }
}

