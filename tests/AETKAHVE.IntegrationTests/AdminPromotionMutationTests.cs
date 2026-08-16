using System.Net;
using System.Net.Http.Json;
using AETKAHVE.Domain.Commerce;
using AETKAHVE.Infrastructure.Persistence;
using AETKAHVE.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AETKAHVE.IntegrationTests;

public sealed class AdminPromotionMutationTests(AeternumWebApplicationFactory factory)
    : IClassFixture<AeternumWebApplicationFactory>
{
    [Fact]
    public async Task Campaign_accepts_empty_and_numeric_optional_values_and_reports_duplicate_slug()
    {
        using var client = await CreateAdminClientAsync();
        var (_, token) = await FormClient.GetFormAsync(client, "/admin/campaigns");
        var suffix = Guid.NewGuid().ToString("N");

        var emptyOptionals = await PostJsonAsync(client, "/admin/campaigns", token, new
        {
            id = (Guid?)null,
            name = $"Boş Limit {suffix}",
            slug = $"bos-limit-{suffix}",
            discountType = (int)DiscountType.Percentage,
            discountValue = 12.5m,
            minimumCartAmount = (decimal?)null,
            maximumDiscountAmount = (decimal?)null,
            startDateUtc = factory.Clock.GetUtcNow(),
            endDateUtc = factory.Clock.GetUtcNow().AddDays(7),
            isActive = true,
            canCombineWithOtherDiscounts = false,
            productIds = (Guid[]?)null,
            categoryIds = (Guid[]?)null,
        });
        Assert.Equal(HttpStatusCode.OK, emptyOptionals.StatusCode);

        var numericOptionals = await PostJsonAsync(client, "/admin/campaigns", token, new
        {
            id = (Guid?)null,
            name = $"Sayısal Limit {suffix}",
            slug = $"sayisal-limit-{suffix}",
            discountType = (int)DiscountType.FixedAmount,
            discountValue = 30m,
            minimumCartAmount = 100m,
            maximumDiscountAmount = 75m,
            startDateUtc = factory.Clock.GetUtcNow(),
            endDateUtc = factory.Clock.GetUtcNow().AddDays(8),
            isActive = true,
            canCombineWithOtherDiscounts = true,
            productIds = Array.Empty<Guid>(),
            categoryIds = Array.Empty<Guid>(),
        });
        Assert.Equal(HttpStatusCode.OK, numericOptionals.StatusCode);

        var duplicate = await PostJsonAsync(client, "/admin/campaigns", token, new
        {
            id = (Guid?)null,
            name = "Aynı Slug",
            slug = $"sayisal-limit-{suffix}",
            discountType = (int)DiscountType.FreeShipping,
            discountValue = 0m,
            minimumCartAmount = (decimal?)null,
            maximumDiscountAmount = (decimal?)null,
            startDateUtc = factory.Clock.GetUtcNow(),
            endDateUtc = factory.Clock.GetUtcNow().AddDays(9),
            isActive = true,
            canCombineWithOtherDiscounts = false,
            productIds = (Guid[]?)null,
            categoryIds = (Guid[]?)null,
        });
        var duplicateBody = await duplicate.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Contains("zaten kullanılıyor", duplicateBody, StringComparison.OrdinalIgnoreCase);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saved = await dbContext.Campaigns
            .Include(x => x.Products)
            .Include(x => x.Categories)
            .SingleAsync(x => x.Slug == $"sayisal-limit-{suffix}");
        Assert.Equal(100m, saved.MinimumCartAmount);
        Assert.Equal(75m, saved.MaximumDiscountAmount);
        Assert.Empty(saved.Products);
        Assert.Empty(saved.Categories);
    }

    [Fact]
    public async Task Coupon_accepts_empty_and_numeric_limits_and_reports_duplicate_code()
    {
        using var client = await CreateAdminClientAsync();
        var (_, token) = await FormClient.GetFormAsync(client, "/admin/coupons");
        var suffix = Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();

        var emptyLimits = await PostJsonAsync(client, "/admin/coupons", token, CouponBody($"BOS{suffix}", null, null));
        Assert.Equal(HttpStatusCode.OK, emptyLimits.StatusCode);

        var numericLimits = await PostJsonAsync(client, "/admin/coupons", token, CouponBody($"SAYI{suffix}", 500, 2));
        Assert.Equal(HttpStatusCode.OK, numericLimits.StatusCode);

        var duplicate = await PostJsonAsync(client, "/admin/coupons", token, CouponBody($"SAYI{suffix}", null, null));
        var duplicateBody = await duplicate.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Contains("zaten kullanılıyor", duplicateBody, StringComparison.OrdinalIgnoreCase);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saved = await dbContext.Coupons.SingleAsync(x => x.Code == $"SAYI{suffix}");
        Assert.Equal(500, saved.TotalUsageLimit);
        Assert.Equal(2, saved.PerUserUsageLimit);
    }

    private object CouponBody(string code, int? totalUsageLimit, int? perUserUsageLimit) => new
    {
        id = (Guid?)null,
        name = $"Kupon {code}",
        code,
        discountType = (int)DiscountType.Percentage,
        discountValue = 10m,
        minimumCartAmount = (decimal?)null,
        maximumDiscountAmount = (decimal?)null,
        startDateUtc = factory.Clock.GetUtcNow(),
        endDateUtc = factory.Clock.GetUtcNow().AddDays(7),
        totalUsageLimit,
        perUserUsageLimit,
        isFirstOrderOnly = false,
        isActive = true,
        canCombineWithOtherDiscounts = false,
    };

    private async Task<HttpClient> CreateAdminClientAsync()
    {
        var client = factory.CreateClientWithoutRedirects();
        var login = await FormClient.LoginAsync(client, "/admin", AeternumWebApplicationFactory.AdminEmail);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        return client;
    }

    private static Task<HttpResponseMessage> PostJsonAsync(HttpClient client, string path, string token, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("RequestVerificationToken", token);
        return client.SendAsync(request);
    }
}
