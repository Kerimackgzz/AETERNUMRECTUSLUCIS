using AETKAHVE.Application.Commerce;
using AETKAHVE.Application.Security;
using AETKAHVE.Domain.Common;
using AETKAHVE.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AETKAHVE.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.CustomerOnly)]
[Route("favorites")]
public sealed class FavoritesController(IFavoriteService favoriteService) : CommerceControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] int page, CancellationToken cancellationToken) =>
        View(new FavoritePageViewModel(await favoriteService.GetAsync(RequiredUserId, Math.Max(1, page), 24, cancellationToken)));

    [HttpPost("{productId:guid}/toggle")]
    public async Task<IActionResult> Toggle(Guid productId, CancellationToken cancellationToken)
    {
        try
        {
            var active = await favoriteService.ToggleAsync(RequiredUserId, productId, cancellationToken);
            return Ok(new CommerceMutationResponse(true, active ? "Favorilere eklendi." : "Favorilerden çıkarıldı.", Data: new { isFavorite = active }));
        }
        catch (CommerceRuleException exception) { return Conflict(new CommerceMutationResponse(false, exception.Message)); }
    }
}
