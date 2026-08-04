using AETKAHVE.Application.Security;
using AETKAHVE.Infrastructure.Identity;
using AETKAHVE.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace AETKAHVE.IntegrationTests.Infrastructure;

public sealed class AeternumWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Password = "ValidPassword1!";
    public const string CustomerEmail = "customer@test.local";
    public const string ResetEmail = "reset@test.local";
    public const string LockoutEmail = "lockout@test.local";
    public const string AdminEmail = "admin@test.local";
    public const string SuperAdminEmail = "superadmin@test.local";

    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public MutableTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 8, 4, 12, 0, 0, TimeSpan.Zero));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
        });
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = "Server=(localdb)\\mssqllocaldb;Database=AETKAHVE.Tests;Trusted_Connection=True;TrustServerCertificate=True",
                ["IdentitySeed:Enabled"] = "false",
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<TimeProvider>();
            services.RemoveAll<IDataProtectionProvider>();
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            services.AddSingleton<TimeProvider>(Clock);
            services.AddSingleton(_connection);
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
        });
    }

    public async Task InitializeAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        foreach (var role in RoleNames.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                Assert.True((await roleManager.CreateAsync(new ApplicationRole { Name = role })).Succeeded);
            }
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await CreateUserAsync(userManager, CustomerEmail, RoleNames.Customer);
        await CreateUserAsync(userManager, ResetEmail, RoleNames.Customer);
        await CreateUserAsync(userManager, LockoutEmail, RoleNames.Admin);
        await CreateUserAsync(userManager, AdminEmail, RoleNames.Admin);
        await CreateUserAsync(userManager, SuperAdminEmail, RoleNames.SuperAdmin);
    }

    Task IAsyncLifetime.DisposeAsync()
    {
        Dispose();
        _connection.Dispose();
        return Task.CompletedTask;
    }

    public HttpClient CreateClientWithoutRedirects() => CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = true,
    });

    private async Task CreateUserAsync(UserManager<ApplicationUser> userManager, string email, string role)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = "Test",
            LastName = role,
            CreatedAtUtc = Clock.GetUtcNow(),
            IsActive = true,
        };
        Assert.True((await userManager.CreateAsync(user, Password)).Succeeded);
        Assert.True((await userManager.AddToRoleAsync(user, role)).Succeeded);
    }
}
