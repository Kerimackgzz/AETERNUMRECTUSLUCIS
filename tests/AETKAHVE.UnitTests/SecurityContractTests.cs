using AETKAHVE.Application.Security;

namespace AETKAHVE.UnitTests;

public sealed class SecurityContractTests
{
    [Fact]
    public void Roles_schemes_and_policies_are_unique()
    {
        Assert.Equal(3, RoleNames.All.Distinct(StringComparer.Ordinal).Count());
        Assert.NotEqual(AuthenticationSchemes.Customer, AuthenticationSchemes.Admin);
        Assert.NotEqual(AuthenticationSchemes.Admin, AuthenticationSchemes.SuperAdmin);
        Assert.NotEqual(AuthorizationPolicies.AdminArea, AuthorizationPolicies.SuperAdminArea);
    }
}

