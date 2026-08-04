using AETKAHVE.Application.Commerce;
using AETKAHVE.Domain.Commerce;
using AETKAHVE.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AETKAHVE.Infrastructure.Commerce;

public sealed class CatalogQueryService(AppDbContext dbContext, TimeProvider timeProvider) : ICatalogQueryService
{
    public async Task<PagedResult<ProductSummary>> SearchAsync(ProductQuery query, Guid? userId, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var products = dbContext.Products.AsNoTracking().Where(x => x.IsActive);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            products = products.Where(x => x.Name.Contains(term) || x.Description.Contains(term) ||
                (x.Brand != null && x.Brand.Name.Contains(term)) || x.Category.Name.Contains(term) ||
                (x.Origin != null && x.Origin.Name.Contains(term)));
        }

        if (query.CategoryId is not null) products = products.Where(x => x.CategoryId == query.CategoryId);
        if (query.CoffeeTypeId is not null) products = products.Where(x => x.CoffeeTypeId == query.CoffeeTypeId);
        if (query.BeanTypeId is not null) products = products.Where(x => x.BeanTypeId == query.BeanTypeId);
        if (query.RoastLevelId is not null) products = products.Where(x => x.RoastLevelId == query.RoastLevelId);
        if (query.OriginId is not null) products = products.Where(x => x.OriginId == query.OriginId);
        if (query.MinimumPrice is not null) products = products.Where(x => (x.DiscountedPrice ?? x.BasePrice) >= query.MinimumPrice);
        if (query.MaximumPrice is not null) products = products.Where(x => (x.DiscountedPrice ?? x.BasePrice) <= query.MaximumPrice);
        if (query.InStockOnly) products = products.Where(x => x.Variants.Any(v => v.IsActive && v.StockQuantity > 0) || (!x.Variants.Any(v => v.IsActive) && x.StockQuantity > 0));
        if (query.DiscountedOnly) products = products.Where(x => x.DiscountedPrice != null && x.DiscountedPrice < x.BasePrice);

        var isSqlite = dbContext.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;
        products = query.Sort.ToLowerInvariant() switch
        {
            "newest" when isSqlite => products.OrderByDescending(x => x.Id),
            "newest" => products.OrderByDescending(x => x.CreatedAtUtc),
            "price-asc" => products.OrderBy(x => x.DiscountedPrice ?? x.BasePrice),
            "price-desc" => products.OrderByDescending(x => x.DiscountedPrice ?? x.BasePrice),
            "rating" => products.OrderByDescending(x => dbContext.Reviews.Where(r => r.ProductId == x.Id && r.Status == ReviewStatus.Approved).Average(r => (double?)r.Rating) ?? 0),
            _ => products.OrderByDescending(x => x.IsFeatured).ThenBy(x => x.Name),
        };

        var total = await products.CountAsync(cancellationToken);
        var items = await ProjectSummaries(products)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var pageIds = items.Select(x => x.Id).ToArray();
        var favoriteIds = userId is null || pageIds.Length == 0
            ? []
            : await dbContext.Favorites.AsNoTracking().Where(x => x.UserId == userId && pageIds.Contains(x.ProductId))
                .Select(x => x.ProductId).ToListAsync(cancellationToken);

        return new PagedResult<ProductSummary>(items.Select(x => x with { IsFavorite = favoriteIds.Contains(x.Id) }).ToList(), page, pageSize, total);
    }

    public async Task<ProductDetails?> GetBySlugAsync(string slug, Guid? userId, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products.AsNoTracking()
            .Include(x => x.Category).Include(x => x.Brand).Include(x => x.CoffeeType)
            .Include(x => x.BeanType).Include(x => x.RoastLevel).Include(x => x.Origin)
            .Include(x => x.Images).Include(x => x.Variants)
            .SingleOrDefaultAsync(x => x.IsActive && x.Slug == slug, cancellationToken);
        if (product is null) return null;

        var reviews = await dbContext.Reviews.AsNoTracking()
            .Where(x => x.ProductId == product.Id && x.Status == ReviewStatus.Approved)
            .Select(x => x.Rating).ToListAsync(cancellationToken);
        var favorite = userId is not null && await dbContext.Favorites.AsNoTracking()
            .AnyAsync(x => x.UserId == userId && x.ProductId == product.Id, cancellationToken);

        return new ProductDetails(
            product.Id, product.Name, product.Slug, product.Sku, product.ShortDescription, product.Description,
            product.Category.Name, product.Brand?.Name, product.CoffeeType?.Name, product.BeanType?.Name,
            product.RoastLevel?.Name, product.Origin?.Name, product.CurrentPrice,
            product.DiscountedPrice is null ? null : product.BasePrice, product.TaxRate,
            product.Variants.Any(x => x.IsActive) ? product.Variants.Where(x => x.IsActive).Sum(x => x.StockQuantity) : product.StockQuantity,
            product.Images.OrderBy(x => x.DisplayOrder).Select(x => (ToUrl(x.StorageKey), x.AltText, x.IsPrimary)).ToList(),
            product.Variants.Where(x => x.IsActive).OrderBy(x => x.Weight)
                .Select(x => new ProductVariantDetails(x.Id, x.Weight, x.Unit, x.Sku, x.CurrentPrice, x.DiscountedPrice is null ? null : x.Price, x.StockQuantity)).ToList(),
            reviews.Count == 0 ? 0 : Math.Round((decimal)reviews.Average(), 2), reviews.Count, favorite);
    }

    public async Task<IReadOnlyList<ProductSummary>> GetFeaturedAsync(int count, Guid? userId, CancellationToken cancellationToken)
    {
        var take = Math.Clamp(count, 1, 24);
        var query = dbContext.Products.AsNoTracking().Where(x => x.IsActive && x.IsFeatured);
        query = dbContext.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true
            ? query.OrderBy(x => x.Name)
            : query.OrderByDescending(x => x.UpdatedAtUtc);
        var items = await ProjectSummaries(query)
            .Take(take).ToListAsync(cancellationToken);
        if (userId is null) return items;

        var ids = items.Select(x => x.Id).ToArray();
        var favorites = await dbContext.Favorites.AsNoTracking()
            .Where(x => x.UserId == userId && ids.Contains(x.ProductId)).Select(x => x.ProductId).ToListAsync(cancellationToken);
        return items.Select(x => x with { IsFavorite = favorites.Contains(x.Id) }).ToList();
    }

    public async Task<IReadOnlyList<CatalogLookupItem>> GetCategoriesAsync(CancellationToken cancellationToken) =>
        await dbContext.Categories.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name)
            .Select(x => new CatalogLookupItem(x.Id, x.Name, x.Slug)).ToListAsync(cancellationToken);

    public async Task<CatalogLookupSet> GetLookupSetAsync(CancellationToken cancellationToken) => new(
        await LookupAsync(dbContext.Categories, cancellationToken),
        await LookupAsync(dbContext.Brands, cancellationToken),
        await LookupAsync(dbContext.CoffeeTypes, cancellationToken),
        await LookupAsync(dbContext.BeanTypes, cancellationToken),
        await LookupAsync(dbContext.RoastLevels, cancellationToken),
        await LookupAsync(dbContext.Origins, cancellationToken));

    public async Task<IReadOnlyList<CampaignSummary>> GetActiveCampaignsAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var source = dbContext.Campaigns.AsNoTracking().Where(x => x.IsActive);
        if (dbContext.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
        {
            return (await source.ToListAsync(cancellationToken))
                .Where(x => x.StartDateUtc <= now && x.EndDateUtc >= now)
                .OrderBy(x => x.Name)
                .Select(x => new CampaignSummary(x.Id, x.Name, x.Slug, x.DiscountType, x.DiscountValue, x.EndDateUtc))
                .ToList();
        }

        return await source.Where(x => x.StartDateUtc <= now && x.EndDateUtc >= now)
            .OrderBy(x => x.EndDateUtc)
            .Select(x => new CampaignSummary(x.Id, x.Name, x.Slug, x.DiscountType, x.DiscountValue, x.EndDateUtc))
            .ToListAsync(cancellationToken);
    }

    internal static IQueryable<ProductSummary> ProjectSummaries(IQueryable<Product> products) =>
        products.Select(x => new ProductSummary(
            x.Id, x.Name, x.Slug,
            "/" + (x.Images.Where(i => i.IsPrimary).Select(i => i.StorageKey).FirstOrDefault() ?? "images/products/placeholder.webp"),
            x.Images.Where(i => i.IsPrimary).Select(i => i.AltText).FirstOrDefault() ?? x.Name,
            x.Category.Name, x.Origin == null ? string.Empty : x.Origin.Name,
            x.RoastLevel == null ? string.Empty : x.RoastLevel.Name,
            x.DiscountedPrice ?? x.BasePrice, x.DiscountedPrice == null ? null : x.BasePrice,
            x.DiscountedPrice != null && x.DiscountedPrice < x.BasePrice,
            x.Variants.Any(v => v.IsActive) ? x.Variants.Any(v => v.IsActive && v.StockQuantity > 0) : x.StockQuantity > 0,
            false));

    private static string ToUrl(string storageKey) => "/" + storageKey.TrimStart('/');
    private static Task<List<CatalogLookupItem>> LookupAsync<T>(DbSet<T> set, CancellationToken cancellationToken) where T : CatalogLookup =>
        set.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name)
            .Select(x => new CatalogLookupItem(x.Id, x.Name, x.Slug)).ToListAsync(cancellationToken);
}
