using AETKAHVE.Application.Commerce;
using AETKAHVE.Domain.Commerce;
using AETKAHVE.Domain.Common;
using AETKAHVE.Infrastructure.Options;
using AETKAHVE.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AETKAHVE.Infrastructure.Commerce;

public sealed class DiscountEngine(AppDbContext dbContext, IOptions<CommerceOptions> options, TimeProvider timeProvider) : IDiscountEngine
{
    private readonly CommerceOptions _options = options.Value;

    public async Task<CartSummary> PriceAsync(Cart cart, Guid? userId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var campaignQuery = dbContext.Campaigns.AsNoTracking()
            .Include(x => x.Products).Include(x => x.Categories)
            .Where(x => x.IsActive);
        var activeCampaigns = dbContext.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true
            ? (await campaignQuery.ToListAsync(cancellationToken)).Where(x => x.StartDateUtc <= now && x.EndDateUtc >= now).ToList()
            : await campaignQuery.Where(x => x.StartDateUtc <= now && x.EndDateUtc >= now).ToListAsync(cancellationToken);

        var rawLines = cart.Items.Where(x => x.Product.IsActive && x.Product.DeletedAtUtc == null).Select(item =>
        {
            var variant = item.ProductVariant;
            var price = variant?.CurrentPrice ?? item.Product.CurrentPrice;
            var stock = variant?.StockQuantity ?? item.Product.StockQuantity;
            var sku = variant?.Sku ?? item.Product.Sku;
            var variantName = variant is null ? null : $"{variant.Weight:0.##} {variant.Unit}";
            return new PricedLine(item, price, stock, sku, variantName);
        }).ToList();

        var subtotal = rawLines.Sum(x => x.UnitPrice * x.Item.Quantity);
        var baseShipping = ShippingFor(subtotal);
        var campaign = activeCampaigns.Where(x => IsCampaignApplicable(x, rawLines, subtotal)).Select(x =>
            {
                var discount = CalculateCampaignDiscount(x, rawLines, subtotal);
                var campaignShipping = x.DiscountType == DiscountType.FreeShipping &&
                                       (x.MinimumCartAmount is null || subtotal >= x.MinimumCartAmount)
                    ? 0
                    : ShippingFor(subtotal - discount);
                return new CampaignPrice(x, discount, campaignShipping, discount + baseShipping - campaignShipping);
            })
            .OrderByDescending(x => x.TotalSavings)
            .ThenBy(x => x.Campaign.EndDateUtc)
            .FirstOrDefault();
        var campaignDiscount = campaign?.Discount ?? 0;
        var shipping = campaign?.Shipping ?? baseShipping;

        decimal couponDiscount = 0;
        if (!string.IsNullOrWhiteSpace(cart.CouponCode))
        {
            var coupon = await dbContext.Coupons.AsNoTracking().Include(x => x.Usages)
                .SingleOrDefaultAsync(x => x.Code == cart.CouponCode, cancellationToken)
                ?? throw new CommerceRuleException("Coupon is invalid.");
            ValidateCoupon(coupon, userId, subtotal, now);
            if (campaign is not null && (!campaign.Campaign.CanCombineWithOtherDiscounts || !coupon.CanCombineWithOtherDiscounts))
            {
                throw new CommerceRuleException("Coupon cannot be combined with the active campaign.");
            }

            if (coupon.DiscountType == DiscountType.FreeShipping) shipping = 0;
            else couponDiscount = CalculateDiscount(coupon.DiscountType, coupon.DiscountValue, coupon.MaximumDiscountAmount, subtotal - campaignDiscount);
        }

        var totalDiscount = Math.Min(subtotal, campaignDiscount + couponDiscount);
        var remainingDiscount = totalDiscount;
        var lines = new List<CartLine>(rawLines.Count);
        foreach (var line in rawLines)
        {
            var lineSubtotal = line.UnitPrice * line.Item.Quantity;
            var share = subtotal == 0 ? 0 : Math.Round(totalDiscount * lineSubtotal / subtotal, 2, MidpointRounding.AwayFromZero);
            share = Math.Min(remainingDiscount, share);
            remainingDiscount -= share;
            var tax = Math.Round((lineSubtotal - share) * line.Item.Product.TaxRate / 100m, 2, MidpointRounding.AwayFromZero);
            lines.Add(new CartLine(line.Item.Id, line.Item.ProductId, line.Item.ProductVariantId, line.Item.Product.Name,
                line.VariantName, line.Sku, line.Item.Quantity, line.Stock, line.UnitPrice, lineSubtotal, share, tax,
                lineSubtotal - share + tax, "/" + (line.Item.Product.Images.Where(x => x.IsPrimary).Select(x => x.StorageKey).FirstOrDefault() ?? "images/products/placeholder.webp")));
        }

        if (lines.Count > 0 && remainingDiscount != 0)
        {
            var last = lines[^1];
            var adjustedDiscount = last.DiscountAmount + remainingDiscount;
            var adjustedTax = Math.Round((last.LineSubtotal - adjustedDiscount) * cart.Items.Single(x => x.Id == last.ItemId).Product.TaxRate / 100m, 2, MidpointRounding.AwayFromZero);
            lines[^1] = last with { DiscountAmount = adjustedDiscount, TaxAmount = adjustedTax, LineTotal = last.LineSubtotal - adjustedDiscount + adjustedTax };
        }

        var taxTotal = lines.Sum(x => x.TaxAmount);
        var warnings = rawLines.Where(x => x.Item.Quantity > x.Stock).Select(x => $"{x.Item.Product.Name} için yeterli stok yok.").ToList();
        return new CartSummary(cart.Id, lines, subtotal, totalDiscount, taxTotal, shipping,
            Math.Max(0, subtotal - totalDiscount + taxTotal + shipping), _options.Currency, cart.CouponCode, warnings);
    }

    private static decimal CalculateCampaignDiscount(Campaign campaign, IReadOnlyCollection<PricedLine> lines, decimal subtotal)
    {
        if (campaign.DiscountType == DiscountType.FreeShipping) return 0;
        var eligible = lines.Where(x => campaign.Products.Count == 0 && campaign.Categories.Count == 0 ||
                campaign.Products.Any(p => p.ProductId == x.Item.ProductId) ||
                campaign.Categories.Any(c => c.CategoryId == x.Item.Product.CategoryId))
            .Sum(x => x.UnitPrice * x.Item.Quantity);
        return CalculateDiscount(campaign.DiscountType, campaign.DiscountValue, campaign.MaximumDiscountAmount, eligible);
    }

    private static bool IsCampaignApplicable(Campaign campaign, IReadOnlyCollection<PricedLine> lines, decimal subtotal) =>
        (campaign.MinimumCartAmount is null || subtotal >= campaign.MinimumCartAmount) &&
        (campaign.Products.Count == 0 && campaign.Categories.Count == 0 || lines.Any(x =>
            campaign.Products.Any(p => p.ProductId == x.Item.ProductId) ||
            campaign.Categories.Any(c => c.CategoryId == x.Item.Product.CategoryId)));

    private static decimal CalculateDiscount(DiscountType type, decimal value, decimal? maximum, decimal amount)
    {
        var discount = type == DiscountType.Percentage ? amount * value / 100m : value;
        if (maximum is not null) discount = Math.Min(discount, maximum.Value);
        return Math.Clamp(Math.Round(discount, 2, MidpointRounding.AwayFromZero), 0, amount);
    }

    private void ValidateCoupon(Coupon coupon, Guid? userId, decimal subtotal, DateTimeOffset now)
    {
        if (!coupon.IsActive || coupon.StartDateUtc > now || coupon.EndDateUtc < now) throw new CommerceRuleException("Coupon is not active.");
        if (coupon.MinimumCartAmount is not null && subtotal < coupon.MinimumCartAmount) throw new CommerceRuleException("Cart minimum is not met.");
        if (coupon.TotalUsageLimit is not null && coupon.Usages.Count(x => x.Status == CouponUsageStatus.Consumed) >= coupon.TotalUsageLimit) throw new CommerceRuleException("Coupon usage limit has been reached.");
        if (userId is not null && coupon.PerUserUsageLimit is not null && coupon.Usages.Count(x => x.UserId == userId && x.Status == CouponUsageStatus.Consumed) >= coupon.PerUserUsageLimit) throw new CommerceRuleException("User coupon usage limit has been reached.");
        if (coupon.IsFirstOrderOnly && (userId is null || dbContext.Orders.Any(x => x.UserId == userId && x.PaymentStatus == PaymentStatus.Succeeded))) throw new CommerceRuleException("Coupon is only valid for a first order.");
    }

    private decimal ShippingFor(decimal discountedSubtotal) =>
        _options.FreeShippingThreshold > 0 && discountedSubtotal >= _options.FreeShippingThreshold ? 0 : _options.ShippingFee;

    private sealed record PricedLine(CartItem Item, decimal UnitPrice, int Stock, string Sku, string? VariantName);
    private sealed record CampaignPrice(Campaign Campaign, decimal Discount, decimal Shipping, decimal TotalSavings);
}
