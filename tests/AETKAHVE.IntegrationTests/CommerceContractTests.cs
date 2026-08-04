using System.Net;
using AETKAHVE.Application.Commerce;
using AETKAHVE.Web.Controllers;
using AETKAHVE.IntegrationTests.Infrastructure;
using AETKAHVE.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace AETKAHVE.IntegrationTests;

public sealed class CommerceContractTests(AeternumWebApplicationFactory factory) : IClassFixture<AeternumWebApplicationFactory>
{
    [Fact]
    public void Frozen_home_view_models_keep_their_exact_public_shape()
    {
        AssertShape<HomePageViewModel>(new Dictionary<string, Type>
        {
            ["FeaturedProducts"] = typeof(IReadOnlyList<ProductCardViewModel>),
            ["HeroFrameManifestUrl"] = typeof(string),
            ["HeroPosterUrl"] = typeof(string),
            ["HeroTitle"] = typeof(string),
            ["HeroSubtitle"] = typeof(string),
            ["HeroAccessibilityDescription"] = typeof(string),
            ["IsReducedMotionFallbackAvailable"] = typeof(bool),
        });
        AssertShape<ProductCardViewModel>(new Dictionary<string, Type>
        {
            ["Id"] = typeof(Guid), ["Name"] = typeof(string), ["Slug"] = typeof(string),
            ["PrimaryImageUrl"] = typeof(string), ["PrimaryImageAlt"] = typeof(string),
            ["CategoryName"] = typeof(string), ["OriginName"] = typeof(string), ["RoastLevelName"] = typeof(string),
            ["DisplayPrice"] = typeof(decimal), ["OriginalPrice"] = typeof(decimal?),
            ["IsDiscounted"] = typeof(bool), ["IsInStock"] = typeof(bool), ["IsFavorite"] = typeof(bool),
            ["AddToCartUrl"] = typeof(string), ["ToggleFavoriteUrl"] = typeof(string), ["DetailUrl"] = typeof(string),
        });
    }

    [Fact]
    public async Task Public_home_renders_the_hero_and_navbar_motion_contract()
    {
        using var client = factory.CreateClientWithoutRedirects();

        var homeResponse = await client.GetAsync("/");
        var html = await homeResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, homeResponse.StatusCode);
        Assert.Contains("data-navbar", html, StringComparison.Ordinal);
        Assert.Contains("/css/components/navbar.css", html, StringComparison.Ordinal);
        Assert.Contains("/js/components/navbar-motion.js", html, StringComparison.Ordinal);
        Assert.Contains("data-frame-manifest-url=\"/frames/home/manifest.json\"", html, StringComparison.Ordinal);
        Assert.Contains("data-reduced-motion=\"false\"", html, StringComparison.Ordinal);

        var navbarCss = await client.GetAsync("/css/components/navbar.css");
        var navbarScript = await client.GetAsync("/js/components/navbar-motion.js");

        Assert.Equal(HttpStatusCode.OK, navbarCss.StatusCode);
        Assert.Equal(HttpStatusCode.OK, navbarScript.StatusCode);
        Assert.Contains("[data-navbar].is-scrolled", await navbarCss.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Contains("classList.toggle(\"is-scrolled\"", await navbarScript.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public void All_public_customer_and_admin_route_families_are_mapped()
    {
        var routes = factory.Services.GetServices<EndpointDataSource>().SelectMany(x => x.Endpoints)
            .OfType<RouteEndpoint>().Select(x => x.RoutePattern.RawText).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        string[] expected =
        [
            "products", "products/{slug}", "search", "categories/{slug}", "campaigns", "cart", "favorites", "checkout",
            "payments/{provider}/callback", "account/addresses", "account/orders", "account/invoices",
            "account/returns", "account/reviews", "account/notifications", "admin/products", "admin/catalog", "admin/orders", "admin/invoices",
            "admin/shipments", "admin/returns", "admin/campaigns", "admin/coupons", "admin/reviews", "admin/messages", "admin/reports",
        ];
        foreach (var route in expected)
            Assert.Contains(routes, mapped => string.Equals(mapped, route, StringComparison.OrdinalIgnoreCase) || mapped.StartsWith(route + "/", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Commerce_mutations_require_antiforgery_except_verified_provider_callback()
    {
        using var client = factory.CreateClientWithoutRedirects();
        var cart = await client.PostAsync("/cart/clear", new FormUrlEncodedContent([]));
        Assert.Equal(HttpStatusCode.BadRequest, cart.StatusCode);

        var callback = await client.PostAsync("/payments/Mock/callback", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["reference"] = "unknown", ["transactionId"] = "unknown", ["status"] = "fail",
        }));
        Assert.Equal(HttpStatusCode.Conflict, callback.StatusCode);
    }

    [Fact]
    public async Task Anonymous_admin_commerce_routes_use_existing_admin_policy_challenge()
    {
        using var client = factory.CreateClientWithoutRedirects();
        var response = await client.GetAsync("/admin/orders");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/admin/login", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task Home_product_card_mutation_and_detail_urls_are_generated_server_side()
    {
        var product = new ProductSummary(Guid.NewGuid(), "Test", "test-product", "/image.webp", "Test", "Coffee", "ET", "Medium", 100, null, false, true, false);
        var controller = new HomeController(new CatalogStub(product))
        {
            Url = new NullUrlHelper(),
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        var result = Assert.IsType<ViewResult>(await controller.Index(default));
        var card = Assert.Single(Assert.IsType<HomePageViewModel>(result.Model).FeaturedProducts);
        Assert.Equal("/cart/items", card.AddToCartUrl);
        Assert.Equal($"/favorites/{product.Id}/toggle", card.ToggleFavoriteUrl);
        Assert.Equal("/products/test-product", card.DetailUrl);
    }

    private static void AssertShape<T>(IReadOnlyDictionary<string, Type> expected)
    {
        var actual = typeof(T).GetProperties().ToDictionary(x => x.Name, x => x.PropertyType, StringComparer.Ordinal);
        Assert.Equal(expected.Count, actual.Count);
        foreach (var property in expected) Assert.Equal(property.Value, actual[property.Key]);
    }

    private sealed class CatalogStub(ProductSummary product) : ICatalogQueryService
    {
        public Task<IReadOnlyList<ProductSummary>> GetFeaturedAsync(int count, Guid? userId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProductSummary>>([product]);
        public Task<PagedResult<ProductSummary>> SearchAsync(ProductQuery query, Guid? userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ProductDetails?> GetBySlugAsync(string slug, Guid? userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<CatalogLookupItem>> GetCategoriesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CatalogLookupSet> GetLookupSetAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<CampaignSummary>> GetActiveCampaignsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class NullUrlHelper : IUrlHelper
    {
        public ActionContext ActionContext { get; } = new();
        public string? Action(UrlActionContext actionContext) => null;
        public string? Content(string? contentPath) => contentPath;
        public bool IsLocalUrl(string? url) => true;
        public string? Link(string? routeName, object? values) => null;
        public string? RouteUrl(UrlRouteContext routeContext) => null;
    }
}
