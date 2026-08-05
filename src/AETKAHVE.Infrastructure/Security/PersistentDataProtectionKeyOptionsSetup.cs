using AETKAHVE.Infrastructure.Options;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AETKAHVE.Infrastructure.Security;

internal sealed class PersistentDataProtectionKeyOptionsSetup(
    IOptions<DataProtectionKeyRingOptions> keyRingOptions,
    IHostEnvironment environment,
    ILoggerFactory loggerFactory) : IConfigureOptions<KeyManagementOptions>
{
    public void Configure(KeyManagementOptions options)
    {
        var configuredPath = keyRingOptions.Value.KeyRingPath.Trim();
        var resolvedPath = Path.GetFullPath(
            Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(environment.ContentRootPath, configuredPath));

        var directory = Directory.CreateDirectory(resolvedPath);
        options.XmlRepository = new FileSystemXmlRepository(directory, loggerFactory);
    }
}
