using AETKAHVE.Application.Commerce;
using AETKAHVE.Application.Security;
using AETKAHVE.Web.Infrastructure;
using AETKAHVE.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace AETKAHVE.Web.Controllers;

[TypeFilter(typeof(GuestCartMergeFilter))]
public sealed class HomeController(ICatalogQueryService catalogQueryService) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = User.TryGetCustomerId(out var id) ? id : (Guid?)null;
        var featured = await catalogQueryService.GetFeaturedAsync(8, userId, cancellationToken);
        return View(new HomePageViewModel
        {
            FeaturedProducts = featured.Select(x => new ProductCardViewModel
            {
                Id = x.Id,
                Name = x.Name,
                Slug = x.Slug,
                PrimaryImageUrl = x.ImageUrl,
                PrimaryImageAlt = x.ImageAlt,
                CategoryName = x.CategoryName,
                OriginName = x.OriginName,
                RoastLevelName = x.RoastLevelName,
                DisplayPrice = x.DisplayPrice,
                OriginalPrice = x.OriginalPrice,
                IsDiscounted = x.IsDiscounted,
                IsInStock = x.IsInStock,
                IsFavorite = x.IsFavorite,
                AddToCartUrl = Url.Action("Add", "Cart") ?? "/cart/items",
                ToggleFavoriteUrl = Url.Action("Toggle", "Favorites", new { productId = x.Id }) ?? $"/favorites/{x.Id}/toggle",
                DetailUrl = Url.Action("Detail", "Products", new { slug = x.Slug }) ?? $"/products/{x.Slug}",
            }).ToList(),
        });
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [HttpGet("/about")]
    public IActionResult About() => View(new AboutPageViewModel());

}
