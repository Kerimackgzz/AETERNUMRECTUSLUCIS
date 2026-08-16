using AETKAHVE.Application.Commerce;
using AETKAHVE.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace AETKAHVE.Web.Controllers;

[Route("products")]
public sealed class ProductsController(ICatalogQueryService catalogQueryService) : CommerceControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] ProductQuery query, CancellationToken cancellationToken)
    {
        var products = await catalogQueryService.SearchAsync(query, CurrentUserId, cancellationToken);
        var lookups = await catalogQueryService.GetLookupSetAsync(cancellationToken);
        return View(new ProductListViewModel(products, lookups, query));
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> Detail(string slug, CancellationToken cancellationToken)
    {
        var product = await catalogQueryService.GetBySlugAsync(slug, CurrentUserId, cancellationToken);
        return product is null ? NotFound() : View(new ProductDetailPageViewModel(product));
    }
}

[Route("search")]
public sealed class SearchController(ICatalogQueryService catalogQueryService) : CommerceControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] ProductQuery query, CancellationToken cancellationToken) =>
        View("~/Views/Products/Index.cshtml", new ProductListViewModel(
            await catalogQueryService.SearchAsync(query, CurrentUserId, cancellationToken),
            await catalogQueryService.GetLookupSetAsync(cancellationToken), query));
}

[Route("campaigns")]
public sealed class CampaignsController(ICatalogQueryService catalogQueryService) : CommerceControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) => View(new CampaignListViewModel(await catalogQueryService.GetActiveCampaignsAsync(cancellationToken)));
}

[Route("categories")]
public sealed class CategoriesController(ICatalogQueryService catalogQueryService) : CommerceControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var categories = await catalogQueryService.GetCategoriesAsync(cancellationToken);
        return View(new CategoryListViewModel(categories
            .Select(c => new CollectionCardViewModel(c.Id, c.Name, c.Slug))
            .ToList()));
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> Detail(string slug, [FromQuery] int page, CancellationToken cancellationToken)
    {
        var category = (await catalogQueryService.GetCategoriesAsync(cancellationToken)).SingleOrDefault(x => x.Slug == slug);
        if (category is null) return NotFound();
        var query = new ProductQuery(CategoryId: category.Id, Page: Math.Max(1, page));
        return View(new ProductListViewModel(await catalogQueryService.SearchAsync(query, CurrentUserId, cancellationToken),
            await catalogQueryService.GetLookupSetAsync(cancellationToken), query));
    }
}
