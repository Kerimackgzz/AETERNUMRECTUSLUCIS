using System.Security.Claims;

namespace AETKAHVE.Application.Security;

public static class CustomerPrincipalExtensions
{
    public static bool IsCustomerPortal(this ClaimsPrincipal principal) =>
        principal.Identity?.IsAuthenticated == true &&
        principal.IsInRole(RoleNames.Customer) &&
        principal.HasClaim(
            SecurityClaimTypes.Portal,
            AuthenticationPortal.Customer.ToString());

    public static bool TryGetCustomerId(this ClaimsPrincipal principal, out Guid userId)
    {
        userId = default;
        return principal.IsCustomerPortal() &&
               Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    }
}
