using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AETKAHVE.Infrastructure.Options;

public sealed class PaymentOptionsValidator(IHostEnvironment environment) : IValidateOptions<PaymentOptions>
{
    public ValidateOptionsResult Validate(string? name, PaymentOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.Provider))
        {
            return ValidateOptionsResult.Fail("Payment:Provider is required.");
        }

        if (options.Provider.Equals(PaymentProviderNames.Mock, StringComparison.OrdinalIgnoreCase))
        {
            return environment.IsDevelopment() || environment.IsEnvironment("Testing")
                ? ValidateOptionsResult.Success
                : ValidateOptionsResult.Fail("The Mock payment provider is restricted to Development and Testing environments.");
        }

        if (options.Provider.Equals(PaymentProviderNames.Disabled, StringComparison.OrdinalIgnoreCase))
        {
            return ValidateOptionsResult.Success;
        }

        if (options.Provider.Equals(PaymentProviderNames.Stripe, StringComparison.OrdinalIgnoreCase))
        {
            return ValidateOptionsResult.Success;
        }

        return ValidateOptionsResult.Fail(
            $"Payment provider '{options.Provider}' has no registered production adapter. Configure a supported provider or use Disabled to fail closed.");
    }
}
