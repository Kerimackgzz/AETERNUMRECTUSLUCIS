using AETKAHVE.Infrastructure.Options;

namespace AETKAHVE.UnitTests;

public sealed class SecurityOptionsValidatorTests
{
    private readonly SecurityOptionsValidator _validator = new();

    [Fact]
    public void Defaults_are_valid()
    {
        var result = _validator.Validate(null, new SecurityOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Equal_management_routes_are_rejected()
    {
        var options = new SecurityOptions { AdminRoute = "management", SuperAdminRoute = "management" };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("must be different", StringComparison.Ordinal));
    }

    [Fact]
    public void Unsafe_route_segments_are_rejected()
    {
        var options = new SecurityOptions { AdminRoute = "admin/login" };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
    }

    [Fact]
    public void Non_positive_rate_limits_are_rejected()
    {
        var options = new SecurityOptions { PasswordRecoveryRequestsPerMinute = 0 };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains(
            nameof(SecurityOptions.PasswordRecoveryRequestsPerMinute),
            StringComparison.Ordinal));
    }
}

