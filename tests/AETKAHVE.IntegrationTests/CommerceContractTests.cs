using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using AETKAHVE.Application.Commerce;
using AETKAHVE.Domain.Commerce;
using AETKAHVE.Infrastructure.Persistence;
using AETKAHVE.Web.Controllers;
using AETKAHVE.IntegrationTests.Infrastructure;
using AETKAHVE.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
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
    public async Task Public_home_renders_the_hero_navbar_and_page_transition_contract()
    {
        using var client = factory.CreateClientWithoutRedirects();

        var homeResponse = await client.GetAsync("/");
        var html = await homeResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, homeResponse.StatusCode);
        Assert.Contains("data-navbar", html, StringComparison.Ordinal);
        Assert.Contains("/css/components/navbar.css", html, StringComparison.Ordinal);
        Assert.Contains("/js/components/navbar-motion.js", html, StringComparison.Ordinal);
        Assert.Contains("/css/core/page-transition.css", html, StringComparison.Ordinal);
        Assert.Contains("/js/core/page-transition.js", html, StringComparison.Ordinal);
        Assert.Contains("data-page-transition-overlay", html, StringComparison.Ordinal);
        Assert.Contains("data-frame-manifest-url=\"/frames/home/manifest.json\"", html, StringComparison.Ordinal);
        Assert.Contains("data-poster-url=\"/frames/home/desktop/poster.webp\"", html, StringComparison.Ordinal);
        Assert.Contains("data-reduced-motion=\"false\"", html, StringComparison.Ordinal);

        var navbarCss = await client.GetAsync("/css/components/navbar.css");
        var navbarScript = await client.GetAsync("/js/components/navbar-motion.js");
        var transitionCss = await client.GetAsync("/css/core/page-transition.css");
        var transitionScript = await client.GetAsync("/js/core/page-transition.js");
        var heroPoster = await client.GetAsync("/frames/home/desktop/poster.webp");

        Assert.Equal(HttpStatusCode.OK, navbarCss.StatusCode);
        Assert.Equal(HttpStatusCode.OK, navbarScript.StatusCode);
        Assert.Equal(HttpStatusCode.OK, transitionCss.StatusCode);
        Assert.Equal(HttpStatusCode.OK, transitionScript.StatusCode);
        Assert.Equal(HttpStatusCode.OK, heroPoster.StatusCode);
        var navbarCssText = await navbarCss.Content.ReadAsStringAsync();
        var navbarScriptText = await navbarScript.Content.ReadAsStringAsync();
        Assert.Contains("[data-navbar].is-scrolled", navbarCssText, StringComparison.Ordinal);
        Assert.Contains(".navbar-brand__letter-mask", navbarCssText, StringComparison.Ordinal);
        Assert.Contains("classList.toggle(\"is-scrolled\"", navbarScriptText, StringComparison.Ordinal);
        Assert.Contains("navbar-brand__letter-mask", navbarScriptText, StringComparison.Ordinal);
        Assert.Contains("[data-page-transition-overlay].is-active", await transitionCss.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Contains("initPageTransitionOverlay", await transitionScript.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public void All_public_customer_and_admin_route_families_are_mapped()
    {
        var routes = factory.Services.GetServices<EndpointDataSource>().SelectMany(x => x.Endpoints)
            .OfType<RouteEndpoint>().Select(x => x.RoutePattern.RawText).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        string[] expected =
        [
            "products", "products/{slug}", "search", "categories", "categories/{slug}", "about", "contact", "campaigns", "cart", "favorites", "checkout",
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
    public async Task Public_navigation_destinations_render_and_contact_json_is_persisted()
    {
        using var client = factory.CreateClientWithoutRedirects();
        foreach (var path in new[] { "/categories", "/about", "/contact" })
        {
            var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var contactResponse = await client.GetAsync("/contact");
        var contactHtml = await contactResponse.Content.ReadAsStringAsync();
        Assert.Contains("data-contact-form", contactHtml, StringComparison.Ordinal);
        Assert.Contains("/js/pages/contact.js", contactHtml, StringComparison.Ordinal);
        var antiforgery = Regex.Match(contactHtml, "<meta name=\"csrf-token\" content=\"([^\"]+)\"", RegexOptions.IgnoreCase);
        Assert.True(antiforgery.Success, "Contact antiforgery meta token was not rendered.");

        var email = $"contact-{Guid.NewGuid():N}@test.local";
        using var request = CreateJsonMutation("/contact", antiforgery.Groups[1].Value, new
        {
            fullName = "Contract Contact",
            email,
            phoneNumber = "+905551112233",
            subject = "Integration contract",
            message = "Public contact form JSON payload.",
            privacyAccepted = true,
        });
        var submitResponse = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.ContactMessages.AnyAsync(x => x.Email == email));
    }

    [Fact]
    public async Task Cart_json_mutations_bind_body_and_select_an_available_default_variant()
    {
        var now = DateTimeOffset.UtcNow;
        var token = Guid.NewGuid().ToString("N");
        var category = new Category
        {
            Name = $"Category {token}", Slug = $"category-{token}", CreatedAtUtc = now, UpdatedAtUtc = now,
        };
        var product = new Product
        {
            Name = $"Product {token}", Slug = $"product-{token}", Sku = $"SKU-{token}", ShortDescription = "Test",
            Description = "JSON cart contract product", BasePrice = 100, TaxRate = 0, StockQuantity = 0,
            Category = category, IsActive = true, CreatedAtUtc = now, UpdatedAtUtc = now,
        };
        var variant = new ProductVariant
        {
            Product = product, Weight = 250, Unit = WeightUnit.Gram, Sku = $"VAR-{token}", Price = 100,
            StockQuantity = 5, IsActive = true, CreatedAtUtc = now, UpdatedAtUtc = now,
        };
        product.Variants.Add(variant);
        await using (var seedScope = factory.Services.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Products.Add(product);
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClientWithoutRedirects();
        var productsResponse = await client.GetAsync("/products");
        productsResponse.EnsureSuccessStatusCode();
        var html = await productsResponse.Content.ReadAsStringAsync();
        var antiforgery = Regex.Match(html, "<meta name=\"csrf-token\" content=\"([^\"]+)\"", RegexOptions.IgnoreCase);
        Assert.True(antiforgery.Success, "Commerce antiforgery meta token was not rendered.");

        using var addRequest = CreateJsonMutation("/cart/items", antiforgery.Groups[1].Value,
            new { productId = product.Id, variantId = (Guid?)null, quantity = 1 });
        var addResponse = await client.SendAsync(addRequest);

        Assert.Equal(HttpStatusCode.OK, addResponse.StatusCode);
        var addResult = Assert.IsType<CommerceMutationResponse>(await addResponse.Content.ReadFromJsonAsync<CommerceMutationResponse>());
        Assert.True(addResult.Success);
        Assert.Equal(1, addResult.CartItemCount);

        Guid itemId;
        await using (var assertionScope = factory.Services.CreateAsyncScope())
        {
            var db = assertionScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var item = await db.CartItems.SingleAsync(x => x.ProductId == product.Id);
            itemId = item.Id;
            Assert.Equal(variant.Id, item.ProductVariantId);
        }

        using var updateRequest = CreateJsonMutation($"/cart/items/{itemId}/quantity", antiforgery.Groups[1].Value, new { quantity = 2 });
        var updateResponse = await client.SendAsync(updateRequest);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        await using var finalScope = factory.Services.CreateAsyncScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(2, (await finalDb.CartItems.SingleAsync(x => x.Id == itemId)).Quantity);
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

    private static HttpRequestMessage CreateJsonMutation(string path, string antiforgeryToken, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("RequestVerificationToken", antiforgeryToken);
        return request;
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
