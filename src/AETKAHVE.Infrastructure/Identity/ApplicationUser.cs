using Microsoft.AspNetCore.Identity;

namespace AETKAHVE.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public DateOnly? DateOfBirth { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? LastLoginAtUtc { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset? DeletedAtUtc { get; set; }
}

public sealed class ApplicationRole : IdentityRole<Guid>
{
}

