using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AETKAHVE.Infrastructure.Options;

public sealed class StripeOptionsValidator(
    IHostEnvironment environment,
    IOptions<PaymentOptions> paymentOptions) : IValidateOptions<StripeOptions>
{
    public ValidateOptionsResult Validate(string? name, StripeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!paymentOptions.Value.Provider.Equals(PaymentProviderNames.Stripe, StringComparison.OrdinalIgnoreCase))
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.SecretKey))
        {
            failures.Add("Stripe:SecretKey is required when Payment:Provider is Stripe.");
        }

        if (environment.IsProduction() && string.IsNullOrWhiteSpace(options.WebhookSecret))
        {
            failures.Add("Stripe:WebhookSecret is required in Production so incoming webhooks can be signature-verified.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
