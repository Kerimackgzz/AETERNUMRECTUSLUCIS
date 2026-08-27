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

        var beanRobusta = await FindOrAddAsync(db.BeanTypes, "2760e806-f47e-4e97-b7f5-6801547a1a20", "Robusta", "robusta", seededAt, cancellationToken);
        var beanBlend = await FindOrAddAsync(db.BeanTypes, "472ca533-b3de-4214-88d6-f38c45a533d9", "Arabica-Robusta Harmanı", "arabica-robusta-harmani", seededAt, cancellationToken);
        var roastLight = await FindOrAddAsync(db.RoastLevels, "78fbfab8-192d-48ab-81a6-34d3542d7389", "Açık Kavrum", "acik-kavrum", seededAt, cancellationToken);
        var roastDark = await FindOrAddAsync(db.RoastLevels, "41d14e55-d035-4251-83be-7576aa5859f7", "Koyu Kavrum", "koyu-kavrum", seededAt, cancellationToken);
        var originCo = await FindOrAddAsync(db.Origins, "f6cfd91b-801f-41b2-8c6e-ac6051402fee", "Kolombiya", "kolombiya", seededAt, cancellationToken);
        originCo.CountryCode ??= "CO";
        var originBr = await FindOrAddAsync(db.Origins, "da9e47dc-9c4b-408c-a3d6-d68862930bfb", "Brezilya", "brezilya", seededAt, cancellationToken);
        originBr.CountryCode ??= "BR";
        var originGt = await FindOrAddAsync(db.Origins, "7598dea9-4cf5-4d3c-8cc4-323ffd9cfdb6", "Guatemala", "guatemala", seededAt, cancellationToken);
        originGt.CountryCode ??= "GT";
        var originKe = await FindOrAddAsync(db.Origins, "3f38289d-8d25-4787-83a6-f1d2886e0984", "Kenya", "kenya", seededAt, cancellationToken);
        originKe.CountryCode ??= "KE";
        var originYe = await FindOrAddAsync(db.Origins, "b6e38cd9-c6a6-491c-bfc8-125c3de8e26a", "Yemen", "yemen", seededAt, cancellationToken);
        originYe.CountryCode ??= "YE";
        var typeFilter = await FindOrAddAsync(db.CoffeeTypes, "9759fc5b-a9ac-49bc-b7e5-849622f0fe11", "Filtre Öğütüm", "filtre-ogutum", seededAt, cancellationToken);
        var typeEspresso = await FindOrAddAsync(db.CoffeeTypes, "6989d2e5-c5bb-4425-8e97-a6b448fc7a0d", "Espresso Öğütüm", "espresso-ogutum", seededAt, cancellationToken);
        var typeTurkish = await FindOrAddAsync(db.CoffeeTypes, "20b19f51-00f0-4c3b-beff-3da927b86a91", "Türk Öğütüm", "turk-ogutum", seededAt, cancellationToken);

        var catSingleOrigin = await FindOrAddAsync(db.Categories, "d4ef592c-5aea-4fe7-9b84-a4fa8683dc3b", "Tek Menşe Kahveler", "tek-mense-kahveler", seededAt, cancellationToken);
        var catBlend = await FindOrAddAsync(db.Categories, "ca927263-ad6d-4120-afee-5901e35a9b0e", "Özel Harman Kahveler", "ozel-harman-kahveler", seededAt, cancellationToken);
        var catEspresso = await FindOrAddAsync(db.Categories, "846d466b-1f2c-4601-bb8c-1326aed6ec0b", "Espresso Kahveleri", "espresso-kahveleri", seededAt, cancellationToken);
        var catFilter = await FindOrAddAsync(db.Categories, "2bd9f61e-3957-464d-aa0c-5487b04c1ce6", "Filtre Kahveler", "filtre-kahveler", seededAt, cancellationToken);
        var catTurkish = await FindOrAddAsync(db.Categories, "e0b83867-b7d4-479e-a803-0609cb5b324b", "Türk Kahvesi", "turk-kahvesi", seededAt, cancellationToken);
        var catDecaf = await FindOrAddAsync(db.Categories, "d93d1f55-cf26-4193-a179-921a4389b898", "Dekafeine Kahveler", "dekafeine-kahveler", seededAt, cancellationToken);

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
        CoffeeSeed[] coffeeSeeds =
        [
            new(Guid.Parse("3a89a509-a258-4aa7-97c9-c283e5fd3298"), Guid.Parse("c260986f-e35d-4c97-9297-a9e15d9e1ce6"),
                "Kolombiya Zirvesi", "kolombiya-zirvesi", "ARL-KZ-250",
                "Karamelli ve fındıksı, dengeli tek menşe kahve.",
                "Kolombiya yaylalarından, orta kavrulmuş, karamel ve fındık notaları taşıyan dengeli bir tek menşe Arabica.",
                420m, null, 80, false, catSingleOrigin, beanType, roast, originCo, coffeeType),
            new(Guid.Parse("f4db4875-6924-4b9c-bddb-48549dcac2a0"), Guid.Parse("86c30ec1-948a-461a-9704-92f803cc70f7"),
                "Kenya Alevi", "kenya-alevi", "ARL-KA-250",
                "Turunçgil asitliği yüksek, canlı bir Kenya kahvesi.",
                "Açık kavrulmuş Kenya Arabica'sı; turunçgil ve kırmızı meyve notalarıyla canlı, keskin bir fincan sunar.",
                460m, 410m, 60, true, catSingleOrigin, beanType, roastLight, originKe, coffeeType),
            new(Guid.Parse("d1946fb3-7a5a-41d0-8d12-d2aed461b190"), Guid.Parse("8482d53e-9273-4100-95fb-825a36521000"),
                "Guatemala Sükûneti", "guatemala-sukuneti", "ARL-GS-250",
                "Çikolatamsı ve yumuşak, sakinleştirici bir tek menşe.",
                "Guatemala'nın volkanik topraklarından, orta kavrulmuş, çikolata ve fındık ağırlıklı yumuşak bir Arabica.",
                440m, null, 70, false, catSingleOrigin, beanType, roast, originGt, coffeeType),
            new(Guid.Parse("3b6872f6-2f21-45a4-bd50-2eb37f08ed94"), Guid.Parse("d6dd4dc7-b605-4539-baf2-0753f2213f25"),
                "Gece Harmanı", "gece-harmani", "ARL-GH-250",
                "Yoğun gövdeli, koyu kavrulmuş özel harman.",
                "Brezilya kökenli Arabica-Robusta harmanı; koyu kavrum ile yoğun gövde ve uzun süren bir aftertaste sunar.",
                380m, null, 100, true, catBlend, beanBlend, roastDark, originBr, coffeeType),
            new(Guid.Parse("4f1598b1-aaf8-48ac-bb57-8918c6ac562f"), Guid.Parse("ad3da845-e304-442f-93a2-982d9b36ac75"),
                "Sabah Ritüeli", "sabah-ritueli", "ARL-SR-250",
                "Çiçeksi ve hafif, filtre için özel harman.",
                "Etiyopya Arabica'sının çiçeksi inceliğiyle kurulmuş, filtre demleme için öğütülmüş sabah harmanı.",
                360m, 320m, 120, false, catBlend, beanType, roast, origin, typeFilter),
            new(Guid.Parse("3906dfcd-373c-4810-9830-3263a218577e"), Guid.Parse("f114377b-8e86-4702-9bcc-e080709b0530"),
                "Altın Saat Harmanı", "altin-saat-harmani", "ARL-ASH-250",
                "Dengeli tatlılıkta, her ana uyan harman.",
                "Kolombiya ağırlıklı Arabica-Robusta harmanı; orta kavrum ile dengeli tatlılık ve yumuşak bir asitlik sunar.",
                400m, null, 90, false, catBlend, beanBlend, roast, originCo, coffeeType),
            new(Guid.Parse("69c1167a-18d3-41f7-90f6-ea89964211c9"), Guid.Parse("93a26241-fae5-4ef8-829b-62b9a25f0ad8"),
                "Roma Ekspres", "roma-ekspres", "ARL-RE-250",
                "Klasik İtalyan tarzı, yoğun kremalı espresso.",
                "Koyu kavrulmuş Brezilya Robusta'sı, espresso için özel öğütülmüş; yoğun crema ve karamelli bir gövde.",
                410m, null, 85, false, catEspresso, beanRobusta, roastDark, originBr, typeEspresso),
            new(Guid.Parse("6e2aa379-8e5d-4421-8da2-43e057803c35"), Guid.Parse("eb5f3325-6019-4bd9-bbe9-a2c5e92b5add"),
                "Kadife Espresso", "kadife-espresso", "ARL-KE-250",
                "Kadifemsi dokuda, dengeli bir espresso deneyimi.",
                "Guatemala Arabica'sının koyu kavrulmuş hali; espresso öğütümüyle kadifemsi doku ve tatlı bir bitiş sunar.",
                430m, 390m, 75, true, catEspresso, beanType, roastDark, originGt, typeEspresso),
            new(Guid.Parse("4acf98d7-53d2-4c2b-9999-466ab7b8d85d"), Guid.Parse("11c7d23f-e54b-4145-a09d-6cb92f3c57e6"),
                "Onyx Espresso", "onyx-espresso", "ARL-OE-250",
                "Baharatlı ve yoğun, karakterli bir espresso.",
                "Yemen Arabica'sının nadir karakteri; koyu kavrum ve espresso öğütümüyle baharatlı, yoğun bir fincan.",
                470m, null, 55, false, catEspresso, beanType, roastDark, originYe, typeEspresso),
            new(Guid.Parse("a8cf9a43-eb8c-43d9-810b-d8c30439bb5e"), Guid.Parse("4f4927a4-2533-4b22-b11d-cbe2f19f8788"),
                "Berrak Filtre", "berrak-filtre", "ARL-BF-250",
                "Berrak ve meyvemsi, açık kavrum filtre kahvesi.",
                "Etiyopya Arabica'sının açık kavrulmuş hali; filtre öğütümüyle berrak, meyvemsi ve çay gibi hafif bir gövde.",
                390m, null, 95, false, catFilter, beanType, roastLight, origin, typeFilter),
            new(Guid.Parse("11959255-a19a-44de-a0cd-b4607e3a77f0"), Guid.Parse("5a715a95-c795-4f36-9198-1e569bd0e9cf"),
                "Meridyen Filtre", "meridyen-filtre", "ARL-MF-250",
                "Kenya kökenli, dengeli asitlikte filtre kahvesi.",
                "Kenya Arabica'sının orta kavrulmuş hali; filtre demleme için öğütülmüş, dengeli asitlik ve kırmızı meyve notaları.",
                420m, 380m, 65, false, catFilter, beanType, roast, originKe, typeFilter),
            new(Guid.Parse("e6e166c8-7d6a-42b7-a270-79a9642e7cf4"), Guid.Parse("344abbdb-58db-4098-af20-83e62ea58c01"),
                "Ilık Rüzgar", "ilik-ruzgar", "ARL-IR-250",
                "Hafif ve ferahlatıcı, açık kavrum filtre kahvesi.",
                "Kolombiya Arabica'sının açık kavrulmuş hali; filtre öğütümüyle hafif gövdeli, elma ve bal notaları taşıyan bir fincan.",
                400m, null, 88, false, catFilter, beanType, roastLight, originCo, typeFilter),
            new(Guid.Parse("921a796f-2cc8-4361-a551-fdeefc041a10"), Guid.Parse("a88e56d5-393a-4a08-b623-3eb88c42e780"),
                "Divan Usulü", "divan-usulu", "ARL-DU-250",
                "Geleneksel usulde, ince öğütülmüş Türk kahvesi.",
                "Yemen Arabica'sının koyu kavrulmuş hali; ince öğütülmüş, köpüklü ve yoğun geleneksel Türk kahvesi.",
                340m, null, 130, true, catTurkish, beanType, roastDark, originYe, typeTurkish),
            new(Guid.Parse("84406763-955a-4982-adcc-007e16bdbf8c"), Guid.Parse("5a4db2eb-5d24-4b49-b68d-7dbf18ec8f32"),
                "Osmanlı Sırrı", "osmanli-sirri", "ARL-OS-250",
                "Zengin aromalı, koyu kavrulmuş Türk kahvesi.",
                "Etiyopya Arabica'sının koyu kavrulmuş hali; ince öğütülmüş, zengin aroma ve uzun süren bir tat bırakır.",
                360m, 320m, 110, false, catTurkish, beanType, roastDark, origin, typeTurkish),
            new(Guid.Parse("edde1f77-b699-4451-b5f4-a05c916791ed"), Guid.Parse("8ef1c4f8-2b94-4a99-b9c0-cbcb1ddce6b8"),
                "Kum Saati", "kum-saati", "ARL-KS-250",
                "Güçlü ve yoğun, harmanlanmış Türk kahvesi.",
                "Arabica-Robusta harmanının koyu kavrulmuş hali; ince öğütülmüş, güçlü ve yoğun bir Türk kahvesi deneyimi.",
                320m, null, 140, false, catTurkish, beanBlend, roastDark, originBr, typeTurkish),
            new(Guid.Parse("6fdf69b3-234a-4f26-8878-152b2eefae67"), Guid.Parse("0f257564-dcb9-4552-9de3-1e574da8fabb"),
                "Sakin Gece", "sakin-gece", "ARL-SG-250",
                "Kafeinsiz, dengeli ve sakinleştirici filtre kahvesi.",
                "Kolombiya Arabica'sından kafeinsiz işlenmiş, orta kavrulmuş; filtre öğütümüyle akşamlara uygun sakin bir fincan.",
                450m, null, 60, false, catDecaf, beanType, roast, originCo, typeFilter),
            new(Guid.Parse("89f20198-d92d-47d3-9cae-fdc9888c049f"), Guid.Parse("8ab37342-8335-4762-96be-c82071ae5520"),
                "Huzur Demi", "huzur-demi", "ARL-HD-250",
                "Kafeinsiz, yumuşak ve huzurlu çekirdek kahve.",
                "Guatemala Arabica'sından kafeinsiz işlenmiş, orta kavrulmuş çekirdek kahve; yumuşak gövdesiyle güne huzurla eşlik eder.",
                470m, 420m, 50, true, catDecaf, beanType, roast, originGt, coffeeType),
            new(Guid.Parse("b2e0eff6-22f4-48ae-9211-053d82c1e6ac"), Guid.Parse("ddaac81f-95d6-4923-b0ed-2b73ee16b5c8"),
                "Ilıman Espresso", "iliman-espresso", "ARL-IE-250",
                "Kafeinsiz, kremalı ve dengeli bir espresso.",
                "Brezilya Arabica'sından kafeinsiz işlenmiş, koyu kavrulmuş; espresso öğütümüyle kremalı ve dengeli bir tat sunar.",
                460m, null, 45, false, catDecaf, beanType, roastDark, originBr, typeEspresso),
        ];

        foreach (var seed in coffeeSeeds)
        {
            if (await db.Products.IgnoreQueryFilters().AnyAsync(x => x.Id == seed.ProductId, cancellationToken)) continue;

            var coffeeProduct = new Product
            {
                Id = seed.ProductId,
                Name = seed.Name,
                Slug = seed.Slug,
                Sku = seed.Sku,
                ShortDescription = seed.ShortDescription,
                Description = seed.Description,
                BasePrice = seed.BasePrice,
                DiscountedPrice = seed.DiscountedPrice,
                TaxRate = 10m,
                StockQuantity = 0,
                CriticalStockLevel = 10,
                Category = seed.Category,
                Brand = brand,
                CoffeeType = seed.CoffeeType,
                BeanType = seed.BeanType,
                RoastLevel = seed.RoastLevel,
                Origin = seed.Origin,
                IsFeatured = seed.IsFeatured,
                CreatedAtUtc = seededAt,
                UpdatedAtUtc = seededAt,
            };
            coffeeProduct.Variants.Add(new ProductVariant
            {
                Id = seed.VariantId,
                Weight = 250,
                Unit = WeightUnit.Gram,
                Sku = seed.Sku,
                Price = seed.BasePrice,
                DiscountedPrice = seed.DiscountedPrice,
                StockQuantity = seed.StockQuantity,
                CreatedAtUtc = seededAt,
                UpdatedAtUtc = seededAt,
            });
            db.Products.Add(coffeeProduct);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private sealed record CoffeeSeed(
        Guid ProductId, Guid VariantId, string Name, string Slug, string Sku,
        string ShortDescription, string Description, decimal BasePrice, decimal? DiscountedPrice,
        int StockQuantity, bool IsFeatured, Category Category, BeanType BeanType, RoastLevel RoastLevel,
        Origin Origin, CoffeeType CoffeeType);

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
