using System.Net;
using AETKAHVE.Application.Security;
using AETKAHVE.Infrastructure.Identity;
using AETKAHVE.Infrastructure.Persistence;
using AETKAHVE.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AETKAHVE.IntegrationTests;

public sealed class AtomicCustomerIdentityTests(AeternumWebApplicationFactory factory)
    : IClassFixture<AeternumWebApplicationFactory>
{
    [Fact]
    public async Task Repeated_registration_replaces_the_pending_request_and_invalidates_the_old_token()
    {
        var email = $"repeat-{Guid.NewGuid():N}@test.local";
        await using var scope = factory.Services.CreateAsyncScope();
        var registrations = scope.ServiceProvider.GetRequiredService<ICustomerRegistrationService>();

        var first = await registrations.BeginAsync(CreateRequest(email, "First"));
        var second = await registrations.BeginAsync(CreateRequest(email, "Second"));

        Assert.Equal(RegistrationStartStatus.Started, first.Status);
        Assert.Equal(RegistrationStartStatus.Started, second.Status);
        Assert.NotNull(first.Dispatch);
        Assert.NotNull(second.Dispatch);
        Assert.Equal(first.Dispatch.RegistrationId, second.Dispatch.RegistrationId);
        Assert.False((await registrations.ValidateConfirmationAsync(
            first.Dispatch.RegistrationId,
            first.Dispatch.Token)).CanConfirm);
        Assert.True((await registrations.ValidateConfirmationAsync(
            second.Dispatch.RegistrationId,
            second.Dispatch.Token)).CanConfirm);
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await dbContext.PendingCustomerRegistrations.CountAsync(x => x.NormalizedEmail == email.ToUpperInvariant()));
        Assert.False(await dbContext.Users.AnyAsync(x => x.NormalizedEmail == email.ToUpperInvariant()));
    }

    [Fact]
    public async Task Existing_identity_returns_explicit_status_without_creating_a_pending_registration()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var registrations = scope.ServiceProvider.GetRequiredService<ICustomerRegistrationService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var original = Assert.IsType<ApplicationUser>(
            await userManager.FindByEmailAsync(AeternumWebApplicationFactory.AdminEmail));
        var originalHash = original.PasswordHash;

        var result = await registrations.BeginAsync(
            CreateRequest(AeternumWebApplicationFactory.AdminEmail, "Başka"));

        Assert.Equal(RegistrationStartStatus.ExistingAccount, result.Status);
        Assert.Null(result.Dispatch);
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await dbContext.PendingCustomerRegistrations.AnyAsync(
            x => x.NormalizedEmail == AeternumWebApplicationFactory.AdminEmail.ToUpperInvariant()));
        Assert.Equal(originalHash, (await userManager.FindByEmailAsync(AeternumWebApplicationFactory.AdminEmail))?.PasswordHash);
    }

    [Fact]
    public async Task Resend_rotates_the_token_without_creating_a_user()
    {
        var email = $"resend-{Guid.NewGuid():N}@test.local";
        await using var scope = factory.Services.CreateAsyncScope();
        var registrations = scope.ServiceProvider.GetRequiredService<ICustomerRegistrationService>();
        var first = await registrations.BeginAsync(CreateRequest(email, "Resend"));

        var resent = await registrations.ResendAsync(email);

        Assert.NotNull(first.Dispatch);
        Assert.NotNull(resent);
        Assert.False((await registrations.ValidateConfirmationAsync(
            first.Dispatch.RegistrationId,
            first.Dispatch.Token)).CanConfirm);
        Assert.True((await registrations.ValidateConfirmationAsync(
            resent.RegistrationId,
            resent.Token)).CanConfirm);
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Null(await userManager.FindByEmailAsync(email));
    }

    [Fact]
    public async Task Expired_token_and_replayed_token_cannot_create_a_second_user()
    {
        var expiredEmail = $"expired-{Guid.NewGuid():N}@test.local";
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var registrations = scope.ServiceProvider.GetRequiredService<ICustomerRegistrationService>();
            var start = await registrations.BeginAsync(CreateRequest(expiredEmail, "Expired"));
            var expiredDispatch = Assert.IsType<RegistrationDispatch>(start.Dispatch);
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var pending = await dbContext.PendingCustomerRegistrations.SingleAsync(x => x.Id == expiredDispatch.RegistrationId);
            pending.TokenExpiresAtUtc = factory.Clock.GetUtcNow().AddMinutes(-1);
            await dbContext.SaveChangesAsync();

            Assert.Equal(
                RegistrationCompletionStatus.InvalidOrExpired,
                await registrations.CompleteAsync(
                    expiredDispatch.RegistrationId,
                    expiredDispatch.Token,
                    EmptyContext));
            Assert.False(await dbContext.Users.AnyAsync(x => x.NormalizedEmail == expiredEmail.ToUpperInvariant()));
        }

        var replayEmail = $"replay-{Guid.NewGuid():N}@test.local";
        RegistrationDispatch dispatch;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var registrations = scope.ServiceProvider.GetRequiredService<ICustomerRegistrationService>();
            dispatch = Assert.IsType<RegistrationDispatch>((await registrations.BeginAsync(CreateRequest(replayEmail, "Replay"))).Dispatch);
            Assert.Equal(
                RegistrationCompletionStatus.Completed,
                await registrations.CompleteAsync(dispatch.RegistrationId, dispatch.Token, EmptyContext));
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var registrations = scope.ServiceProvider.GetRequiredService<ICustomerRegistrationService>();
            Assert.Equal(
                RegistrationCompletionStatus.InvalidOrExpired,
                await registrations.CompleteAsync(dispatch.RegistrationId, dispatch.Token, EmptyContext));
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(1, await dbContext.Users.CountAsync(x => x.NormalizedEmail == replayEmail.ToUpperInvariant()));
        }
    }

    [Fact]
    public async Task Reset_link_get_is_read_only_and_invalid_post_preserves_the_password_hash()
    {
        string token;
        string originalPasswordHash;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = Assert.IsType<ApplicationUser>(await userManager.FindByEmailAsync(AeternumWebApplicationFactory.ResetEmail));
            token = await userManager.GeneratePasswordResetTokenAsync(user);
            originalPasswordHash = Assert.IsType<string>(user.PasswordHash);
        }

        using var client = factory.CreateClientWithoutRedirects();
        var path = $"/account/reset-password?email={Uri.EscapeDataString(AeternumWebApplicationFactory.ResetEmail)}&token={Uri.EscapeDataString(token)}";
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path)).StatusCode);
        var invalidPost = await FormClient.PostFormAsync(
            client,
            path,
            "/account/reset-password",
            new Dictionary<string, string>
            {
                ["Email"] = AeternumWebApplicationFactory.ResetEmail,
                ["Token"] = "invalid",
                ["Password"] = "AnotherPassword3!",
                ["ConfirmPassword"] = "AnotherPassword3!",
            });
        Assert.Equal(HttpStatusCode.OK, invalidPost.StatusCode);

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationManager = verificationScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var unchanged = Assert.IsType<ApplicationUser>(await verificationManager.FindByEmailAsync(AeternumWebApplicationFactory.ResetEmail));
        Assert.Equal(originalPasswordHash, unchanged.PasswordHash);
    }

    private BeginCustomerRegistration CreateRequest(string email, string firstName) => new(
        firstName,
        "Customer",
        email,
        AeternumWebApplicationFactory.Password,
        factory.Clock.GetUtcNow());

    private static SecurityEventContext EmptyContext { get; } = new(null, null, "/account/confirm-email", "test-correlation");
}
