using AETKAHVE.Infrastructure.Commerce;
using AETKAHVE.Infrastructure.Options;
using AETKAHVE.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AETKAHVE.IntegrationTests;

public sealed class DevelopmentDatabaseTests
{
    [Fact]
    public async Task Sqlite_development_seed_bootstraps_the_schema_and_is_idempotent()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddSingleton(connection);
        services.AddDbContext<AppDbContext>((provider, options) =>
            options.UseSqlite(provider.GetRequiredService<SqliteConnection>()));

        await using var provider = services.BuildServiceProvider();
        var seed = new CommerceSeedHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new DevelopmentHostEnvironment(),
            Options.Create(new CommerceOptions { SeedDevelopmentData = true }));

        await seed.StartAsync(default);

        await using (var repairScope = provider.CreateAsyncScope())
        {
            var repairDb = repairScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seededImage = await repairDb.ProductImages.SingleAsync(x =>
                x.Id == Guid.Parse("2764536f-11e8-49a4-9bc1-5e307553022b"));
            seededImage.StorageKey = "images/products/eternal-light.webp";
            await repairDb.SaveChangesAsync();
        }

        await seed.StartAsync(default);

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.Products.CountAsync(x => x.Id == Guid.Parse("3f411410-3eca-4f51-83ae-c166e2b201e3")));
        Assert.Equal("frames/home/desktop/poster.webp", await db.ProductImages
            .Where(x => x.Id == Guid.Parse("2764536f-11e8-49a4-9bc1-5e307553022b"))
            .Select(x => x.StorageKey)
            .SingleAsync());
        Assert.Equal(1, await db.ProductVariants.CountAsync(x => x.Id == Guid.Parse("d050cf93-efb1-4612-a074-a1956f88f67b")));
        Assert.Equal(1, await db.Campaigns.CountAsync(x => x.Id == Guid.Parse("0ebacaf8-36f8-4678-945b-f6255947406a")));
        Assert.Equal(1, await db.Coupons.CountAsync(x => x.Id == Guid.Parse("15775140-09de-463b-9d3b-1d4e4bb8079d")));
    }

    private sealed class DevelopmentHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "AETKAHVE.IntegrationTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
