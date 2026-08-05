using AETKAHVE.Domain.Commerce;
using AETKAHVE.Infrastructure.Options;
using AETKAHVE.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AETKAHVE.Infrastructure.Commerce;

public sealed class CommerceSeedHostedService(
    IServiceScopeFactory scopeFactory,
    IHostEnvironment environment,
    IOptions<CommerceOptions> options) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment() || !options.Value.SeedDevelopmentData) return;
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
        {
            await db.Database.EnsureCreatedAsync(cancellationToken);
        }

        var seededAt = new DateTimeOffset(2026, 8, 4, 0, 0, 0, TimeSpan.Zero);
        var category = await FindOrAddAsync(db.Categories, "2f97391d-918a-45b5-9f04-489ba4b455a1", "Nitelikli Kahve", "nitelikli-kahve", seededAt, cancellationToken);
        var brand = await FindOrAddAsync(db.Brands, "a9c8f3bd-0891-4948-a4d4-b3dfc1f269b0", "AETERNUM RECTUS LUCIS", "aeternum-rectus-lucis", seededAt, cancellationToken);
        var coffeeType = await FindOrAddAsync(db.CoffeeTypes, "10210855-0691-48dd-a70b-7d43561ed077", "Çekirdek Kahve", "cekirdek-kahve", seededAt, cancellationToken);
        var beanType = await FindOrAddAsync(db.BeanTypes, "72e12e2f-8138-47ea-9fd8-41b15114f1a8", "Arabica", "arabica", seededAt, cancellationToken);
        var roast = await FindOrAddAsync(db.RoastLevels, "87d75345-f5ec-4895-81b6-dfbe7fcba915", "Orta Kavrum", "orta-kavrum", seededAt, cancellationToken);
        var origin = await FindOrAddAsync(db.Origins, "2590682d-3743-414c-ac78-c24b2f9a7f50", "Etiyopya", "etiyopya", seededAt, cancellationToken);
        origin.CountryCode ??= "ET";

        var productId = Guid.Parse("3f411410-3eca-4f51-83ae-c166e2b201e3");
        var product = await db.Products.IgnoreQueryFilters().Include(x => x.Images).Include(x => x.Variants)
            .SingleOrDefaultAsync(x => x.Id == productId, cancellationToken);
        if (product is null)
        {
            product = new Product
            {
                Id = productId,
                Name = "Eternal Light",
                Slug = "eternal-light",
                Sku = "ARL-EL-250",
                ShortDescription = "Meyvemsi ve dengeli nitelikli kahve.",
                Description = "AETERNUM RECTUS LUCIS seçkisinden dengeli bir Etiyopya kahvesi.",
                BasePrice = 480m,
                DiscountedPrice = 430m,
                TaxRate = 0m,
                StockQuantity = 0,
                CriticalStockLevel = 10,
                Category = category,
                Brand = brand,
                CoffeeType = coffeeType,
                BeanType = beanType,
                RoastLevel = roast,
                Origin = origin,
                IsFeatured = true,
                CreatedAtUtc = seededAt,
                UpdatedAtUtc = seededAt,
            };
            product.Variants.Add(new ProductVariant { Id = Guid.Parse("d050cf93-efb1-4612-a074-a1956f88f67b"), Weight = 250, Unit = WeightUnit.Gram, Sku = "ARL-EL-250", Price = 480m, DiscountedPrice = 430m, StockQuantity = 100, CreatedAtUtc = seededAt, UpdatedAtUtc = seededAt });
            db.Products.Add(product);
        }

        var imageId = Guid.Parse("2764536f-11e8-49a4-9bc1-5e307553022b");
        var image = product.Images.SingleOrDefault(x => x.Id == imageId);
        if (image is null)
        {
            product.Images.Add(new ProductImage
            {
                Id = imageId,
                StorageKey = "frames/home/desktop/poster.webp",
                AltText = "Eternal Light kahve paketi",
                IsPrimary = true,
                CreatedAtUtc = seededAt,
                UpdatedAtUtc = seededAt,
            });
        }
        else if (image.StorageKey == "images/products/eternal-light.webp")
        {
            image.StorageKey = "frames/home/desktop/poster.webp";
            image.UpdatedAtUtc = seededAt;
        }

        var campaignId = Guid.Parse("0ebacaf8-36f8-4678-945b-f6255947406a");
        var campaign = await db.Campaigns.Include(x => x.Products).SingleOrDefaultAsync(x => x.Id == campaignId, cancellationToken);
        if (campaign is null)
        {
            campaign = new Campaign
            {
                Id = campaignId,
                Name = "Açılış Ritüeli",
                Slug = "acilis-ritueli",
                DiscountType = DiscountType.Percentage,
                DiscountValue = 10,
                MaximumDiscountAmount = 150,
                StartDateUtc = seededAt.AddYears(-1),
                EndDateUtc = seededAt.AddYears(10),
                IsActive = true,
                CanCombineWithOtherDiscounts = false,
                CreatedAtUtc = seededAt,
                UpdatedAtUtc = seededAt,
            };
            db.Campaigns.Add(campaign);
        }
        if (campaign.Products.All(x => x.ProductId != productId))
        {
            var campaignProduct = new CampaignProduct { Campaign = campaign, CampaignId = campaignId, Product = product, ProductId = productId };
            campaign.Products.Add(campaignProduct);
            db.CampaignProducts.Add(campaignProduct);
        }

        var couponId = Guid.Parse("15775140-09de-463b-9d3b-1d4e4bb8079d");
        if (!await db.Coupons.AnyAsync(x => x.Id == couponId, cancellationToken))
        {
            db.Coupons.Add(new Coupon
            {
                Id = couponId,
                Name = "Hoş Geldiniz",
                Code = "AETERNUM10",
                DiscountType = DiscountType.Percentage,
                DiscountValue = 10,
                MaximumDiscountAmount = 100,
                StartDateUtc = seededAt.AddYears(-1),
                EndDateUtc = seededAt.AddYears(10),
                TotalUsageLimit = 1000,
                PerUserUsageLimit = 1,
                IsActive = true,
                CanCombineWithOtherDiscounts = false,
                CreatedAtUtc = seededAt,
                UpdatedAtUtc = seededAt,
            });
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task<T> FindOrAddAsync<T>(DbSet<T> set, string id, string name, string slug, DateTimeOffset now, CancellationToken cancellationToken)
        where T : CatalogLookup, new()
    {
        var key = Guid.Parse(id);
        var entity = await set.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == key, cancellationToken);
        if (entity is not null) return entity;
        entity = new T { Id = key, Name = name, Slug = slug, CreatedAtUtc = now, UpdatedAtUtc = now };
        set.Add(entity);
        return entity;
    }
}
