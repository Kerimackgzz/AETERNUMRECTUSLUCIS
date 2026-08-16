using AETKAHVE.Infrastructure.Options;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace AETKAHVE.UnitTests;

public sealed class IdentitySeedOptionsValidatorTests
{
    [Fact]
    public void Disabled_seed_is_valid_without_credentials_even_in_production()
    {
        var result = Validator(Environments.Production).Validate(null, new IdentitySeedOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Enabled_seed_requires_complete_email_password_pairs()
    {
        var result = Validator(Environments.Development).Validate(null, new IdentitySeedOptions
        {
            Enabled = true,
            AdminEmail = "management@example.test",
        });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("both be configured", StringComparison.Ordinal));
    }

    [Fact]
    public void Same_normalized_email_requires_matching_passwords_without_exposing_them()
    {
        const string adminPassword = "FirstSecret1!";
        const string superAdminPassword = "SecondSecret2!";
        var result = Validator(Environments.Development).Validate(null, new IdentitySeedOptions
        {
            Enabled = true,
            AdminEmail = " Management@Example.Test ",
            AdminPassword = adminPassword,
            SuperAdminEmail = "management@example.test",
            SuperAdminPassword = superAdminPassword,
        });

        Assert.True(result.Failed);
        var failures = string.Join(" ", result.Failures);
        Assert.Contains("must match", failures, StringComparison.Ordinal);
        Assert.DoesNotContain(adminPassword, failures, StringComparison.Ordinal);
        Assert.DoesNotContain(superAdminPassword, failures, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_seed_requires_explicit_opt_in()
    {
        var options = CompleteOptions();
        options.AdminPassword = "ProductionSecret1234567!";
        options.SuperAdminPassword = "ProductionSecret1234567!";

        var denied = Validator(Environments.Production).Validate(null, options);
        options.AllowInProduction = true;
        var allowed = Validator(Environments.Production).Validate(null, options);

        Assert.True(denied.Failed);
        Assert.Contains(denied.Failures, failure => failure.Contains("AllowInProduction", StringComparison.Ordinal));
        Assert.True(allowed.Succeeded);
    }

    [Fact]
    public void Production_seed_requires_24_character_passwords_without_exposing_them()
    {
        const string shortSecret = "ShortSecret1!";
        var options = CompleteOptions();
        options.AllowInProduction = true;
        options.AdminPassword = shortSecret;
        options.SuperAdminPassword = shortSecret;

        var result = Validator(Environments.Production).Validate(null, options);

        Assert.True(result.Failed);
        var failures = string.Join(" ", result.Failures);
        Assert.Contains("at least 24 characters", failures, StringComparison.Ordinal);
        Assert.DoesNotContain(shortSecret, failures, StringComparison.Ordinal);
    }

    [Fact]
    public void Retirement_requires_destructive_opt_in_and_a_different_replacement()
    {
        var withoutApproval = CompleteOptions();
        withoutApproval.RetireManagementEmails = ["legacy@example.test"];

        var denied = Validator(Environments.Development).Validate(null, withoutApproval);

        Assert.True(denied.Failed);
        Assert.Contains(denied.Failures, failure => failure.Contains("AllowDestructiveRetirement", StringComparison.Ordinal));

        var replacementCollision = CompleteOptions();
        replacementCollision.AllowDestructiveRetirement = true;
        replacementCollision.RetireManagementEmails = [" MANAGEMENT@example.test "];

        var collision = Validator(Environments.Development).Validate(null, replacementCollision);

        Assert.True(collision.Failed);
        Assert.Contains(collision.Failures, failure => failure.Contains("cannot also be retired", StringComparison.Ordinal));
    }

    [Fact]
    public void Retirement_requires_replacements_for_both_management_roles()
    {
        var options = new IdentitySeedOptions
        {
            Enabled = true,
            AllowDestructiveRetirement = true,
            AdminEmail = "management@example.test",
            AdminPassword = "ValidPassword1!",
            RetireManagementEmails = ["legacy@example.test"],
        };

        var result = Validator(Environments.Development).Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains(
            "Both Admin and SuperAdmin replacement roles",
            StringComparison.Ordinal));
    }

    private static IdentitySeedOptions CompleteOptions() => new()
    {
        Enabled = true,
        AdminEmail = "management@example.test",
        AdminPassword = "ValidPassword1!",
        SuperAdminEmail = "MANAGEMENT@example.test",
        SuperAdminPassword = "ValidPassword1!",
    };

    private static IdentitySeedOptionsValidator Validator(string environmentName) =>
        new(new TestHostEnvironment(environmentName));

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "AETKAHVE.UnitTests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
