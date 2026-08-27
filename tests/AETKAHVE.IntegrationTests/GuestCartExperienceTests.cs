using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using AETKAHVE.Application.Commerce;
using AETKAHVE.Application.Security;
using AETKAHVE.Domain.Commerce;
using AETKAHVE.Infrastructure.Commerce;
using AETKAHVE.Infrastructure.Identity;
using AETKAHVE.Infrastructure.Notifications;
using AETKAHVE.Infrastructure.Options;
using AETKAHVE.Infrastructure.Persistence;
using AETKAHVE.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AETKAHVE.IntegrationTests;

public sealed class GuestCartExperienceTests(AeternumWebApplicationFactory factory)
    : IClassFixture<AeternumWebApplicationFactory>
{
    [Fact]
    public async Task Guest_cart_cookie_is_opaque_secure_and_survives_full_cart_mutations()
    {
        var product = await CreateProductAsync(stock: 6);
        using var client = factory.CreateClientWithoutRedirects();
        var token = await GetAntiforgeryTokenAsync(client, "/products");

        using var add = await PostJsonAsync(client, "/cart/items", token,
            new { productId = product.ProductId, variantId = product.VariantId, quantity = 2 });

        Assert.Equal(HttpStatusCode.OK, add.StatusCode);
        var cookie = Assert.Single(add.Headers.GetValues("Set-Cookie"), value =>
            value.StartsWith("AETKAHVE.GuestCart=", StringComparison.Ordinal));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("max-age=2592000", cookie, StringComparison.OrdinalIgnoreCase);
        var cookieValue = cookie["AETKAHVE.GuestCart=".Length..].Split(';')[0];
        Assert.False(Guid.TryParse(WebUtility.UrlDecode(cookieValue), out _));
        Assert.DoesNotContain(product.ProductId.ToString("D"), cookieValue, StringComparison.OrdinalIgnoreCase);

        using var firstCartPage = await client.GetAsync("/cart");
        using var refreshedCartPage = await client.GetAsync("/cart");
        Assert.Contains(product.Name, await firstCartPage.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Contains(product.Name, await refreshedCartPage.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        Guid itemId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            itemId = await scope.ServiceProvider.GetRequiredService<AppDbContext>().CartItems
                .Where(x => x.ProductId == product.ProductId && x.Cart.UserId == null)
                .Select(x => x.Id)
                .SingleAsync();
        }

        using var update = await PostJsonAsync(client, $"/cart/items/{itemId}/quantity", token, new { quantity = 3 });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            Assert.Equal(3, (await scope.ServiceProvider.GetRequiredService<AppDbContext>().CartItems
                .AsNoTracking().SingleAsync(x => x.Id == itemId)).Quantity);
        }

        using var remove = await PostWithoutBodyAsync(client, $"/cart/items/{itemId}/remove", token);
        Assert.Equal(HttpStatusCode.OK, remove.StatusCode);
        await using var assertionScope = factory.Services.CreateAsyncScope();
        Assert.False(await assertionScope.ServiceProvider.GetRequiredService<AppDbContext>().CartItems
            .AnyAsync(x => x.Id == itemId));
    }

    [Fact]
    public async Task Login_merges_guest_and_customer_lines_once_and_caps_quantity_to_stock()
    {
        var product = await CreateProductAsync(stock: 5);
        Guid customerId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            customerId = (await GetUserAsync(scope.ServiceProvider, AeternumWebApplicationFactory.CustomerEmail)).Id;
            var carts = scope.ServiceProvider.GetRequiredService<ICartService>();
            await carts.ClearAsync(new CartOwner(customerId, null), default);
            await carts.AddAsync(new CartOwner(customerId, null), product.ProductId, product.VariantId, 3, default);
        }

        using var client = factory.CreateClientWithoutRedirects();
        var token = await GetAntiforgeryTokenAsync(client, "/products");
        using (var add = await PostJsonAsync(client, "/cart/items", token,
                   new { productId = product.ProductId, variantId = product.VariantId, quantity = 4 }))
        {
            Assert.Equal(HttpStatusCode.OK, add.StatusCode);
        }

        using (var login = await FormClient.LoginAsync(client, "/account", AeternumWebApplicationFactory.CustomerEmail))
            Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        using var account = await client.GetAsync("/account");
        Assert.Equal(HttpStatusCode.OK, account.StatusCode);
        Assert.Contains(account.Headers.GetValues("Set-Cookie"), value =>
            value.StartsWith("AETKAHVE.GuestCart=", StringComparison.Ordinal) &&
            value.Contains("expires=", StringComparison.OrdinalIgnoreCase));

        await using var assertionScope = factory.Services.CreateAsyncScope();
        var db = assertionScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var customerLines = await db.CartItems.AsNoTracking()
            .Where(x => x.Cart.UserId == customerId && x.ProductId == product.ProductId && x.ProductVariantId == product.VariantId)
            .ToListAsync();
        Assert.Equal(5, Assert.Single(customerLines).Quantity);
        Assert.False(await db.Carts.AnyAsync(x => x.GuestToken != null && x.Items.Any(i => i.ProductId == product.ProductId)));

        using var products = await client.GetAsync("/products");
        var html = WebUtility.HtmlDecode(await products.Content.ReadAsStringAsync());
        Assert.Contains("Sepetim", html, StringComparison.Ordinal);
        Assert.Contains("Hesabım", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Giriş Yap / Hesap Oluştur", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/admin", AeternumWebApplicationFactory.AdminEmail)]
    [InlineData("/superadmin", AeternumWebApplicationFactory.SuperAdminEmail)]
    public async Task Management_sessions_remain_guests_on_the_storefront(string portal, string email)
    {
        var before = await GuestCartIdsAsync();
        using var client = factory.CreateClientWithoutRedirects();
        using (var login = await FormClient.LoginAsync(client, portal, email))
            Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        using var response = await client.GetAsync("/cart");
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Sepetim", html, StringComparison.Ordinal);
        Assert.Contains("Giriş Yap / Hesap Oluştur", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">Hesabım<", html, StringComparison.Ordinal);
        var after = await GuestCartIdsAsync();
        Assert.Single(after.Except(before));
    }

    [Fact]
    public async Task Checkout_challenge_and_login_preserve_only_a_local_return_url()
    {
        using var client = factory.CreateClientWithoutRedirects();
        using var challenge = await client.GetAsync("/checkout");
        Assert.Equal(HttpStatusCode.Redirect, challenge.StatusCode);
        Assert.Equal("/account/login", challenge.Headers.Location?.AbsolutePath);
        var challengeQuery = QueryHelpers.ParseQuery(challenge.Headers.Location?.Query ?? string.Empty);
        Assert.Equal("/checkout", challengeQuery["ReturnUrl"].ToString());

        using var login = await FormClient.PostFormAsync(
            client,
            "/account/login?returnUrl=%2Fcheckout",
            "/account/login",
            new Dictionary<string, string>
            {
                ["Email"] = AeternumWebApplicationFactory.CustomerEmail,
                ["Password"] = AeternumWebApplicationFactory.Password,
                ["RememberMe"] = "false",
                ["ReturnUrl"] = "/checkout",
            });
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/checkout", login.Headers.Location?.OriginalString);

        using var externalClient = factory.CreateClientWithoutRedirects();
        using var external = await FormClient.PostFormAsync(
            externalClient,
            "/account/login?returnUrl=https%3A%2F%2Fevil.example%2Fsteal",
            "/account/login",
            new Dictionary<string, string>
            {
                ["Email"] = AeternumWebApplicationFactory.CustomerEmail,
                ["Password"] = AeternumWebApplicationFactory.Password,
                ["RememberMe"] = "false",
                ["ReturnUrl"] = "https://evil.example/steal",
            });
        Assert.Equal("/account", external.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Registration_confirmation_chain_preserves_checkout_return_url()
    {
        var email = $"checkout-{Guid.NewGuid():N}@test.local";
        using var client = factory.CreateClientWithoutRedirects();
        using var registration = await FormClient.PostFormAsync(
            client,
            "/account/register?returnUrl=%2Fcheckout",
            "/account/register",
            new Dictionary<string, string>
            {
                ["FirstName"] = "Checkout",
                ["LastName"] = "Customer",
                ["Email"] = email,
                ["Password"] = AeternumWebApplicationFactory.Password,
                ["ConfirmPassword"] = AeternumWebApplicationFactory.Password,
                ["AcceptPrivacyTerms"] = "true",
                ["ReturnUrl"] = "/checkout",
            });
        var registrationLocation = new Uri(new Uri("https://localhost"), registration.Headers.Location!);
        var registrationQuery = QueryHelpers.ParseQuery(registrationLocation.Query);
        Assert.Equal("/checkout", registrationQuery["returnUrl"].ToString());

        var message = factory.Services.GetRequiredService<InMemoryIdentityMessageSender>().Messages
            .Last(item => string.Equals(item.Destination, email, StringComparison.OrdinalIgnoreCase));
        var linkMatch = Regex.Match(message.HtmlBody, "href=\\\"([^\\\"]+)\\\"");
        Assert.True(linkMatch.Success);
        var confirmationUrl = WebUtility.HtmlDecode(linkMatch.Groups[1].Value);
        Assert.Equal("/checkout", QueryHelpers.ParseQuery(new Uri(confirmationUrl).Query)["returnUrl"].ToString());

        using var confirmationPage = await client.GetAsync(confirmationUrl);
        var confirmationHtml = await confirmationPage.Content.ReadAsStringAsync();
        using var completion = await FormClient.PostWithTokenAsync(
            client,
            "/account/confirm-email",
            ExtractHiddenValue(confirmationHtml, "__RequestVerificationToken"),
            new Dictionary<string, string>
            {
                ["RegistrationId"] = ExtractHiddenValue(confirmationHtml, "RegistrationId"),
                ["Token"] = ExtractHiddenValue(confirmationHtml, "Token"),
                ["ReturnUrl"] = ExtractHiddenValue(confirmationHtml, "ReturnUrl"),
            });
        var completionLocation = new Uri(new Uri("https://localhost"), completion.Headers.Location!);
        var completionQuery = QueryHelpers.ParseQuery(completionLocation.Query);
        Assert.Equal("/checkout", completionQuery["returnUrl"].ToString());

        using var finalLogin = await FormClient.PostFormAsync(
            client,
            completion.Headers.Location!.OriginalString,
            "/account/login",
            new Dictionary<string, string>
            {
                ["Email"] = email,
                ["Password"] = AeternumWebApplicationFactory.Password,
                ["RememberMe"] = "false",
                ["ReturnUrl"] = "/checkout",
            });
        Assert.Equal("/checkout", finalLogin.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Expired_guest_cart_is_not_merged_into_the_customer_cart()
    {
        var product = await CreateProductAsync(stock: 3);
        var guestToken = Guid.NewGuid();
        Guid customerId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            customerId = (await GetUserAsync(services, AeternumWebApplicationFactory.CustomerEmail)).Id;
            var carts = services.GetRequiredService<ICartService>();
            await carts.ClearAsync(new CartOwner(customerId, null), default);
            await carts.AddAsync(new CartOwner(null, guestToken), product.ProductId, product.VariantId, 1, default);
            var db = services.GetRequiredService<AppDbContext>();
            var guestCart = await db.Carts.SingleAsync(x => x.GuestToken == guestToken);
            guestCart.ExpiresAtUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        await using (var mergeScope = factory.Services.CreateAsyncScope())
        {
            var merged = await mergeScope.ServiceProvider.GetRequiredService<ICartService>()
                .MergeGuestCartAsync(customerId, guestToken, default);
            Assert.DoesNotContain(merged.Cart.Items, x => x.ProductId == product.ProductId);
            Assert.NotEmpty(merged.Warnings);
        }

        await using var assertionScope = factory.Services.CreateAsyncScope();
        var assertionDb = assertionScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await assertionDb.Carts.AnyAsync(x => x.GuestToken == guestToken));
        Assert.False(await assertionDb.CartItems.AnyAsync(x => x.Cart.UserId == customerId && x.ProductId == product.ProductId));
    }

    [Fact]
    public async Task Checkout_redirects_to_cart_when_a_variant_becomes_unavailable()
    {
        var product = await CreateProductAsync(stock: 2);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var customer = await GetUserAsync(services, AeternumWebApplicationFactory.CustomerEmail);
            var carts = services.GetRequiredService<ICartService>();
            await carts.ClearAsync(new CartOwner(customer.Id, null), default);
            await carts.AddAsync(new CartOwner(customer.Id, null), product.ProductId, product.VariantId, 1, default);

            var db = services.GetRequiredService<AppDbContext>();
            var variant = await db.ProductVariants.SingleAsync(x => x.Id == product.VariantId);
            variant.IsActive = false;
            variant.UpdatedAtUtc = services.GetRequiredService<TimeProvider>().GetUtcNow();
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClientWithoutRedirects();
        using (var login = await FormClient.LoginAsync(client, "/account", AeternumWebApplicationFactory.CustomerEmail))
            Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        using var checkout = await client.GetAsync("/checkout");

        Assert.Equal(HttpStatusCode.Redirect, checkout.StatusCode);
        Assert.Equal("/cart", checkout.Headers.Location?.OriginalString);
        await using var assertionScope = factory.Services.CreateAsyncScope();
        Assert.False(await assertionScope.ServiceProvider.GetRequiredService<AppDbContext>().CartItems
            .AnyAsync(x => x.ProductVariantId == product.VariantId));
    }

    [Fact]
    public async Task Parallel_guest_adds_keep_one_cart_line_without_lost_updates()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"aetkahve-cart-{Guid.NewGuid():N}.db");
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={databasePath};Pooling=False;Default Timeout=5")
            .Options;
        var commerceOptions = Options.Create(new CommerceOptions
        {
            Currency = "TRY",
            GuestCartLifetimeDays = 30,
            MaximumCartItemQuantity = 20,
        });
        var now = TimeProvider.System.GetUtcNow();
        var productId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var guestToken = Guid.NewGuid();

        try
        {
            await using (var setup = new AppDbContext(dbOptions))
            {
                await setup.Database.EnsureCreatedAsync();
                var suffix = Guid.NewGuid().ToString("N");
                var category = new Category
                {
                    Name = $"Parallel category {suffix}",
                    Slug = $"parallel-category-{suffix}",
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now,
                };
                var product = new Product
                {
                    Id = productId,
                    Name = $"Parallel product {suffix}",
                    Slug = $"parallel-product-{suffix}",
                    Sku = $"PAR-{suffix.ToUpperInvariant()}",
                    ShortDescription = "Parallel cart test",
                    Description = "Parallel cart test product",
                    BasePrice = 100,
                    TaxRate = 0,
                    Category = category,
                    IsActive = true,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now,
                };
                product.Variants.Add(new ProductVariant
                {
                    Id = variantId,
                    Product = product,
                    Weight = 250,
                    Unit = WeightUnit.Gram,
                    Sku = $"PAR-VAR-{suffix.ToUpperInvariant()}",
                    Price = 100,
                    StockQuantity = 20,
                    IsActive = true,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now,
                });
                setup.Products.Add(product);
                await setup.SaveChangesAsync();
            }

            async Task AddOnceAsync()
            {
                await using var db = new AppDbContext(dbOptions);
                var discounts = new DiscountEngine(db, commerceOptions, TimeProvider.System);
                var carts = new CartService(db, discounts, commerceOptions, TimeProvider.System);
                await carts.AddAsync(new CartOwner(null, guestToken), productId, variantId, 1, default);
            }

            await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => AddOnceAsync()));

            await using var assertionDb = new AppDbContext(dbOptions);
            var cart = await assertionDb.Carts.AsNoTracking().Include(x => x.Items)
                .SingleAsync(x => x.GuestToken == guestToken);
            Assert.Equal(4, Assert.Single(cart.Items).Quantity);
        }
        finally
        {
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    private async Task<(Guid ProductId, Guid VariantId, string Name)> CreateProductAsync(int stock)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<AppDbContext>();
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow();
        var token = Guid.NewGuid().ToString("N");
        var category = new Category
        {
            Name = $"Guest category {token}",
            Slug = $"guest-category-{token}",
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        var product = new Product
        {
            Name = $"Guest product {token}",
            Slug = $"guest-product-{token}",
            Sku = $"GUEST-{token.ToUpperInvariant()}",
            ShortDescription = "Guest cart test",
            Description = "Guest cart integration test product",
            BasePrice = 125,
            TaxRate = 10,
            Category = category,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        var variant = new ProductVariant
        {
            Product = product,
            Weight = 250,
            Unit = WeightUnit.Gram,
            Sku = $"GUEST-VAR-{token.ToUpperInvariant()}",
            Price = 125,
            StockQuantity = stock,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        product.Variants.Add(variant);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return (product.Id, variant.Id, product.Name);
    }

    private async Task<HashSet<Guid>> GuestCartIdsAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Carts.AsNoTracking()
            .Where(x => x.UserId == null && x.GuestToken != null)
            .Select(x => x.Id)
            .ToListAsync()).ToHashSet();
    }

    private static async Task<ApplicationUser> GetUserAsync(IServiceProvider services, string email) =>
        await services.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(email)
        ?? throw new InvalidOperationException($"Test user {email} was not found.");

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        var match = Regex.Match(html, "<meta name=\"csrf-token\" content=\"([^\"]+)\"", RegexOptions.IgnoreCase);
        Assert.True(match.Success, $"Commerce antiforgery meta token was not rendered at {path}.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static async Task<HttpResponseMessage> PostJsonAsync(HttpClient client, string path, string token, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("RequestVerificationToken", token);
        var response = await client.SendAsync(request);
        request.Dispose();
        return response;
    }

    private static async Task<HttpResponseMessage> PostWithoutBodyAsync(HttpClient client, string path, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("RequestVerificationToken", token);
        var response = await client.SendAsync(request);
        request.Dispose();
        return response;
    }

    private static string ExtractHiddenValue(string html, string name)
    {
        var input = Regex.Match(
            html,
            $"<input[^>]*name=\"{Regex.Escape(name)}\"[^>]*value=\"([^\"]*)\"[^>]*>",
            RegexOptions.IgnoreCase);
        Assert.True(input.Success, $"Hidden input {name} was not found.");
        return WebUtility.HtmlDecode(input.Groups[1].Value);
    }
}
