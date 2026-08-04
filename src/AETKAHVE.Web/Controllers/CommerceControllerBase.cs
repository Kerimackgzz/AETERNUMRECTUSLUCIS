using System.Security.Claims;
using AETKAHVE.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace AETKAHVE.Web.Controllers;

[TypeFilter(typeof(GuestCartMergeFilter))]
public abstract class CommerceControllerBase : Controller
{
    protected Guid? CurrentUserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    protected Guid RequiredUserId => CurrentUserId ?? throw new InvalidOperationException("Authenticated user id is unavailable.");
}
