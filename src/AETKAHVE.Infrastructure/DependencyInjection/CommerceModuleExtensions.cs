using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AETKAHVE.Infrastructure.DependencyInjection;

public static class CommerceModuleExtensions
{
    public static IServiceCollection AddCommerceModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        return services;
    }
}

