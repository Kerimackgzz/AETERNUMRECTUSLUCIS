using AETKAHVE.Application.Notifications;
using AETKAHVE.Infrastructure.Notifications;
using AETKAHVE.Infrastructure.Options;
using AETKAHVE.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AETKAHVE.Infrastructure.DependencyInjection;

public static class InfrastructureModuleExtensions
{
    public static IServiceCollection AddInfrastructureModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .Validate(x => !string.IsNullOrWhiteSpace(x.ConnectionString), "Database:ConnectionString is required.")
            .ValidateOnStart();

        var databaseOptions = configuration
            .GetSection(DatabaseOptions.SectionName)
            .Get<DatabaseOptions>() ?? new DatabaseOptions();

        services.AddDbContext<AppDbContext>(options => options.UseSqlServer(databaseOptions.ConnectionString));
        services.AddSingleton<InMemoryIdentityMessageSender>();
        services.AddSingleton<IIdentityMessageSender>(provider =>
            provider.GetRequiredService<InMemoryIdentityMessageSender>());
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }
}
