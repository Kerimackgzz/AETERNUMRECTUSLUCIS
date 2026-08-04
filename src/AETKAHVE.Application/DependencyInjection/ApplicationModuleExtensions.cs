using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AETKAHVE.Application.DependencyInjection;

public static class ApplicationModuleExtensions
{
    public static IServiceCollection AddApplicationModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        return services;
    }
}

