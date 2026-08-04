namespace AETKAHVE.Application.Security;

public static class RoleNames
{
    public const string Customer = "Customer";
    public const string Admin = "Admin";
    public const string SuperAdmin = "SuperAdmin";

    public static readonly string[] All = [Customer, Admin, SuperAdmin];
}

public static class AuthenticationSchemes
{
    public const string Customer = "AETKAHVE.Customer";
    public const string Admin = "AETKAHVE.Admin";
    public const string SuperAdmin = "AETKAHVE.SuperAdmin";
    public const string Management = "AETKAHVE.Management";
}

public static class AuthorizationPolicies
{
    public const string CustomerOnly = "CustomerOnly";
    public const string AdminArea = "AdminArea";
    public const string SuperAdminArea = "SuperAdminArea";
}

public static class SecurityClaimTypes
{
    public const string SessionId = "aetkahve:session_id";
    public const string SecurityStamp = "aetkahve:security_stamp";
    public const string Portal = "aetkahve:portal";
}

public static class CookieNames
{
    public const string Customer = "AETKAHVE.Customer.Auth";
    public const string Admin = "AETKAHVE.Admin.Auth";
    public const string SuperAdmin = "AETKAHVE.SuperAdmin.Auth";
}

