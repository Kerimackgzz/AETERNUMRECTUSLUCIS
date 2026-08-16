namespace AETKAHVE.Infrastructure.Options;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

public sealed class IdentitySeedOptions
{
    public const string SectionName = "IdentitySeed";

    public bool Enabled { get; set; }

    public bool AllowInProduction { get; set; }

    public bool AllowDestructiveRetirement { get; set; }

    public string? AdminEmail { get; set; }

    public string? AdminPassword { get; set; }

    public string? SuperAdminEmail { get; set; }

    public string? SuperAdminPassword { get; set; }

    public List<string> RetireManagementEmails { get; set; } = [];
}

public sealed class IdentitySeedOptionsValidator(IHostEnvironment environment)
    : IValidateOptions<IdentitySeedOptions>
{
    public ValidateOptionsResult Validate(string? name, IdentitySeedOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();
        ValidatePair(options.AdminEmail, options.AdminPassword, "Admin", failures);
        ValidatePair(options.SuperAdminEmail, options.SuperAdminPassword, "SuperAdmin", failures);

        if (environment.IsProduction() && !options.AllowInProduction)
        {
            failures.Add(
                $"{IdentitySeedOptions.SectionName}:{nameof(IdentitySeedOptions.AllowInProduction)} must be explicitly enabled before identity seeding can run in Production.");
        }

        if (environment.IsProduction())
        {
            ValidateProductionPassword(options.AdminPassword, "AdminPassword", failures);
            ValidateProductionPassword(options.SuperAdminPassword, "SuperAdminPassword", failures);
        }

        if (HasCompletePair(options.AdminEmail, options.AdminPassword)
            && HasCompletePair(options.SuperAdminEmail, options.SuperAdminPassword)
            && string.Equals(
                NormalizeEmail(options.AdminEmail!),
                NormalizeEmail(options.SuperAdminEmail!),
                StringComparison.Ordinal)
            && !string.Equals(options.AdminPassword, options.SuperAdminPassword, StringComparison.Ordinal))
        {
            failures.Add("AdminPassword and SuperAdminPassword must match when both roles use the same normalized email address.");
        }

        var retirementEmails = options.RetireManagementEmails
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Select(NormalizeEmail)
            .ToHashSet(StringComparer.Ordinal);
        if (retirementEmails.Count > 0)
        {
            if (!options.AllowDestructiveRetirement)
            {
                failures.Add(
                    $"{IdentitySeedOptions.SectionName}:{nameof(IdentitySeedOptions.AllowDestructiveRetirement)} must be explicitly enabled before retiring management users.");
            }

            var replacementEmails = new[] { options.AdminEmail, options.SuperAdminEmail }
                .Where(email => !string.IsNullOrWhiteSpace(email))
                .Select(email => NormalizeEmail(email!))
                .ToHashSet(StringComparer.Ordinal);
            if (!HasCompletePair(options.AdminEmail, options.AdminPassword)
                || !HasCompletePair(options.SuperAdminEmail, options.SuperAdminPassword))
            {
                failures.Add("Both Admin and SuperAdmin replacement roles must be configured before retiring management users.");
            }

            if (retirementEmails.Overlaps(replacementEmails))
            {
                failures.Add("A configured replacement management account cannot also be retired.");
            }
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    internal static bool HasCompletePair(string? email, string? password) =>
        !string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password);

    internal static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();

    private static void ValidatePair(
        string? email,
        string? password,
        string prefix,
        ICollection<string> failures)
    {
        if (string.IsNullOrWhiteSpace(email) == string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        failures.Add($"{prefix}Email and {prefix}Password must either both be configured or both be omitted.");
    }

    private static void ValidateProductionPassword(
        string? password,
        string optionName,
        ICollection<string> failures)
    {
        if (!string.IsNullOrWhiteSpace(password) && password.Length < 24)
        {
            failures.Add($"{optionName} must contain at least 24 characters when identity seeding runs in Production.");
        }
    }
}

