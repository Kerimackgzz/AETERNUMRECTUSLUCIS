using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.RateLimiting;
using AETKAHVE.Infrastructure.DependencyInjection;
using AETKAHVE.Infrastructure.Options;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace AETKAHVE.Web.DependencyInjection;

public static class WebSecurityModuleExtensions
{
    public static IServiceCollection AddWebSecurityModule(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var forwardedHeadersValidator = new ForwardedHeadersSecurityOptionsValidator();
        var forwardedHeaders = configuration
            .GetSection(ForwardedHeadersSecurityOptions.SectionName)
            .Get<ForwardedHeadersSecurityOptions>() ?? new ForwardedHeadersSecurityOptions();
        ThrowIfInvalid(
            ForwardedHeadersSecurityOptions.SectionName,
            forwardedHeaders,
            forwardedHeadersValidator.Validate(null, forwardedHeaders));

        var dataProtectionValidator = new DataProtectionKeyRingOptionsValidator(environment.IsProduction());
        var dataProtection = configuration
            .GetSection(DataProtectionKeyRingOptions.SectionName)
            .Get<DataProtectionKeyRingOptions>() ?? new DataProtectionKeyRingOptions();
        ThrowIfInvalid(
            DataProtectionKeyRingOptions.SectionName,
            dataProtection,
            dataProtectionValidator.Validate(null, dataProtection));

        services.AddSingleton<IValidateOptions<ForwardedHeadersSecurityOptions>, ForwardedHeadersSecurityOptionsValidator>();
        services.AddOptions<ForwardedHeadersSecurityOptions>()
            .Bind(configuration.GetSection(ForwardedHeadersSecurityOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<DataProtectionKeyRingOptions>>(
            new DataProtectionKeyRingOptionsValidator(environment.IsProduction()));
        services.AddOptions<DataProtectionKeyRingOptions>()
            .Bind(configuration.GetSection(DataProtectionKeyRingOptions.SectionName))
            .ValidateOnStart();

        ConfigureForwardedHeaders(services, forwardedHeaders);
        ConfigureDataProtection(services, dataProtection, environment);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds)
                        .ToString(CultureInfo.InvariantCulture);
                }

                return ValueTask.CompletedTask;
            };
            AddIpPolicy(options, SecurityRateLimitPolicies.CustomerLogin, security => security.CustomerLoginRequestsPerMinute);
            AddIpPolicy(options, SecurityRateLimitPolicies.AdminLogin, security => security.AdminLoginRequestsPerMinute);
            AddIpPolicy(options, SecurityRateLimitPolicies.SuperAdminLogin, security => security.SuperAdminLoginRequestsPerMinute);
            AddIpPolicy(options, SecurityRateLimitPolicies.CustomerRegistration, security => security.CustomerRegistrationRequestsPerMinute);
            AddIpPolicy(options, SecurityRateLimitPolicies.PasswordRecovery, security => security.PasswordRecoveryRequestsPerMinute);
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                IsContactMutation(context)
                    ? CreateIpFixedWindowPartition(
                        context,
                        SecurityRateLimitPolicies.Contact,
                        security => security.ContactRequestsPerMinute)
                    : RateLimitPartition.GetNoLimiter("unlimited"));
        });
        return services;
    }

    private static void ConfigureForwardedHeaders(
        IServiceCollection services,
        ForwardedHeadersSecurityOptions configuredOptions)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();

            if (!configuredOptions.Enabled)
            {
                options.ForwardedHeaders = ForwardedHeaders.None;
                return;
            }

            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = configuredOptions.ForwardLimit;
            options.RequireHeaderSymmetry = true;

            foreach (var proxy in configuredOptions.KnownProxies ?? [])
            {
                options.KnownProxies.Add(IPAddress.Parse(proxy));
            }

            foreach (var network in configuredOptions.KnownNetworks ?? [])
            {
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
            }
        });
    }

    private static void ConfigureDataProtection(
        IServiceCollection services,
        DataProtectionKeyRingOptions configuredOptions,
        IHostEnvironment environment)
    {
        var builder = services.AddDataProtection()
            .SetApplicationName(configuredOptions.ApplicationName);

        if (string.IsNullOrWhiteSpace(configuredOptions.KeyRingPath))
        {
            return;
        }

        DirectoryInfo keyRingDirectory;
        try
        {
            keyRingDirectory = Directory.CreateDirectory(configuredOptions.KeyRingPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                $"Data Protection key ring directory '{configuredOptions.KeyRingPath}' could not be created or accessed.",
                exception);
        }

        builder.PersistKeysToFileSystem(keyRingDirectory);

        if (!string.IsNullOrWhiteSpace(configuredOptions.CertificateThumbprint) ||
            !string.IsNullOrWhiteSpace(configuredOptions.CertificatePath))
        {
            builder.ProtectKeysWithCertificate(LoadDataProtectionCertificate(configuredOptions));
            return;
        }

        if (environment.IsProduction())
        {
            throw new InvalidOperationException(
                "Production Data Protection keys must be encrypted with a configured certificate.");
        }
    }

    private static X509Certificate2 LoadDataProtectionCertificate(DataProtectionKeyRingOptions options)
    {
        try
        {
            var certificate = !string.IsNullOrWhiteSpace(options.CertificatePath)
                ? LoadCertificateFromFile(options.CertificatePath, options.CertificatePassword!)
                : LoadCertificateFromStore(options.CertificateThumbprint!);

            if (!certificate.HasPrivateKey)
            {
                throw new InvalidOperationException("The Data Protection certificate does not contain a private key.");
            }

            var now = DateTime.UtcNow;
            if (now < certificate.NotBefore.ToUniversalTime() || now > certificate.NotAfter.ToUniversalTime())
            {
                throw new InvalidOperationException("The Data Protection certificate is not currently valid.");
            }

            return certificate;
        }
        catch (Exception exception) when (exception is CryptographicException or IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                "The configured Data Protection certificate could not be loaded. Check its path/thumbprint, private key access, and secret password.",
                exception);
        }
    }

    private static X509Certificate2 LoadCertificateFromFile(string path, string password)
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Data Protection certificate file '{path}' does not exist.");
        }

        return X509CertificateLoader.LoadPkcs12FromFile(
            path,
            password,
            X509KeyStorageFlags.EphemeralKeySet,
            Pkcs12LoaderLimits.Defaults);
    }

    private static X509Certificate2 LoadCertificateFromStore(string thumbprint)
    {
        var normalizedThumbprint = string.Concat(thumbprint.Where(character => !char.IsWhiteSpace(character)));
        foreach (var location in new[] { StoreLocation.CurrentUser, StoreLocation.LocalMachine })
        {
            using var store = new X509Store(StoreName.My, location);
            store.Open(OpenFlags.ReadOnly);
            var certificate = store.Certificates
                .Find(X509FindType.FindByThumbprint, normalizedThumbprint, validOnly: false)
                .OfType<X509Certificate2>()
                .FirstOrDefault(candidate => candidate.HasPrivateKey);
            if (certificate is not null)
            {
                return certificate;
            }
        }

        throw new InvalidOperationException(
            $"No Data Protection certificate with thumbprint '{normalizedThumbprint}' and an accessible private key was found in the CurrentUser or LocalMachine personal store.");
    }

    private static void ThrowIfInvalid<TOptions>(
        string optionsName,
        TOptions options,
        ValidateOptionsResult result)
    {
        if (result.Failed)
        {
            throw new OptionsValidationException(optionsName, typeof(TOptions), result.Failures);
        }
    }

    private static void AddIpPolicy(
        RateLimiterOptions options,
        string policyName,
        Func<SecurityOptions, int> permitLimit)
    {
        options.AddPolicy(policyName, context => CreateIpFixedWindowPartition(context, policyName, permitLimit));
    }

    private static RateLimitPartition<string> CreateIpFixedWindowPartition(
        HttpContext context,
        string policyName,
        Func<SecurityOptions, int> permitLimit) =>
        RateLimitPartition.GetFixedWindowLimiter(
            $"{policyName}:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit(context.RequestServices.GetRequiredService<IOptions<SecurityOptions>>().Value),
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            });

    private static bool IsContactMutation(HttpContext context) =>
        HttpMethods.IsPost(context.Request.Method) &&
        string.Equals(context.Request.Path.Value, "/contact", StringComparison.OrdinalIgnoreCase);
}

