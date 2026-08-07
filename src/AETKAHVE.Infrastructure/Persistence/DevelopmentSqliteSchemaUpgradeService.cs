using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AETKAHVE.Infrastructure.Persistence;

/// <summary>
/// Keeps an existing developer database created by EnsureCreated usable when a nullable,
/// backwards-compatible profile column is introduced. Production schema changes remain
/// exclusively migration-driven.
/// </summary>
public sealed class DevelopmentSqliteSchemaUpgradeService(
    IServiceScopeFactory scopeFactory,
    IHostEnvironment environment) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment()) return;
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (!dbContext.Database.IsSqlite()) return;

        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);
        if (!await HasColumnAsync(connection, "AspNetUsers", "ProfileImageStorageKey", cancellationToken))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"AspNetUsers\" ADD COLUMN \"ProfileImageStorageKey\" TEXT NULL;",
                cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task<bool> HasColumnAsync(
        DbConnection connection,
        string table,
        string column,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{table}\");";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}
