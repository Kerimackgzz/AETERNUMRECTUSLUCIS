using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AETKAHVE.Infrastructure.Options;

public sealed class PaymentOptionsValidator(IHostEnvironment environment) : IValidateOptions<PaymentOptions>
{
    public ValidateOptionsResult Validate(string? name, PaymentOptions options) =>
        environment.IsProduction()
            ? ValidateOptionsResult.Fail(
                $"Payment:Provider '{options.Provider}' cannot run in Production because no production payment adapter is registered.")
            : ValidateOptionsResult.Success;
}

public sealed class ShippingOptionsValidator(IHostEnvironment environment) : IValidateOptions<ShippingOptions>
{
    public ValidateOptionsResult Validate(string? name, ShippingOptions options) =>
        environment.IsProduction()
            ? ValidateOptionsResult.Fail(
                $"Shipping:Provider '{options.Provider}' cannot run in Production because no production shipping adapter is registered.")
            : ValidateOptionsResult.Success;
}

public sealed class NotificationOptionsValidator(IHostEnvironment environment) : IValidateOptions<NotificationOptions>
{
    public ValidateOptionsResult Validate(string? name, NotificationOptions options)
    {
        var failures = new List<string>();

        if (options.ProcessingLeaseSeconds <= options.PollIntervalSeconds)
        {
            failures.Add("Notifications:ProcessingLeaseSeconds must be greater than PollIntervalSeconds.");
        }

        if (environment.IsProduction() && options.UseMockProviders)
        {
            failures.Add("Notifications:UseMockProviders must be false in Production.");
        }

        if (environment.IsProduction() && !options.EmailDeliveryEnabled)
        {
            failures.Add("Notifications:EmailDeliveryEnabled must be true in Production because Identity messages use the email outbox.");
        }

        if (environment.IsProduction() && options.SmsDeliveryEnabled)
        {
            failures.Add("Notifications:SmsDeliveryEnabled must remain false until a production SMS provider is registered.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}

public sealed class SmtpOptionsValidator(
    IHostEnvironment environment,
    IOptions<NotificationOptions> notificationOptions) : IValidateOptions<SmtpOptions>
{
    public ValidateOptionsResult Validate(string? name, SmtpOptions options)
    {
        if (NotificationProviderSelection.UseMockProviders(environment, notificationOptions.Value))
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.Host))
        {
            failures.Add("Smtp:Host is required when mock notification providers are disabled.");
        }

        if (!options.UseSsl)
        {
            failures.Add("Smtp:UseSsl must be true when mock notification providers are disabled.");
        }

        if (string.IsNullOrWhiteSpace(options.FromName))
        {
            failures.Add("Smtp:FromName is required when mock notification providers are disabled.");
        }

        if (string.IsNullOrWhiteSpace(options.FromAddress) ||
            options.FromAddress.EndsWith(".invalid", StringComparison.OrdinalIgnoreCase))
        {
            failures.Add("Smtp:FromAddress must be a deliverable address when mock notification providers are disabled.");
        }

        var hasUserName = !string.IsNullOrWhiteSpace(options.UserName);
        var hasPassword = !string.IsNullOrWhiteSpace(options.Password);
        if (hasUserName != hasPassword)
        {
            failures.Add("Smtp:UserName and Smtp:Password must either both be configured or both be omitted.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}

internal static class NotificationProviderSelection
{
    public static bool UseMockProviders(IHostEnvironment environment, NotificationOptions options) =>
        environment.IsDevelopment() ||
        environment.IsEnvironment("Testing") ||
        options.UseMockProviders;
}
