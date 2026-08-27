using AETKAHVE.Application.Security;
using AETKAHVE.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace AETKAHVE.Web.Controllers;

[TypeFilter(typeof(GuestCartMergeFilter))]
public abstract class CommerceControllerBase : Controller
{
    protected Guid? CurrentUserId => User.TryGetCustomerId(out var id) ? id : null;

    protected Guid RequiredUserId => CurrentUserId ?? throw new InvalidOperationException("Authenticated user id is unavailable.");
}
