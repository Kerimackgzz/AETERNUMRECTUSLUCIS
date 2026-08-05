using AETKAHVE.Application.Commerce;
using AETKAHVE.Domain.Commerce;
using AETKAHVE.Domain.Common;
using AETKAHVE.Infrastructure.Options;
using AETKAHVE.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AETKAHVE.Infrastructure.Commerce;

public sealed class CartService(
    AppDbContext dbContext,
    IDiscountEngine discountEngine,
    IOptions<CommerceOptions> options,
    TimeProvider timeProvider) : ICartService
{
    private readonly CommerceOptions _options = options.Value;

    public async Task<CartSummary> GetAsync(CartOwner owner, CancellationToken cancellationToken)
    {
        var cart = await GetOrCreateAsync(owner, cancellationToken);
        return await PriceWithCouponRecoveryAsync(cart, owner.UserId, cancellationToken);
    }

    public async Task<CartSummary> AddAsync(CartOwner owner, Guid productId, Guid? variantId, int quantity, CancellationToken cancellationToken)
    {
        if (quantity < 1 || quantity > _options.MaximumCartItemQuantity) throw new CommerceRuleException("Cart quantity is invalid.");
        var product = await dbContext.Products.Include(x => x.Variants)
            .SingleOrDefaultAsync(x => x.Id == productId && x.IsActive, cancellationToken)
            ?? throw new CommerceRuleException("Product is unavailable.");
        ProductVariant? variant;
        if (variantId is null)
        {
            variant = product.Variants
                .Where(x => x.IsActive && x.StockQuantity > 0)
                .OrderBy(x => x.Weight)
                .ThenBy(x => x.Id)
                .FirstOrDefault();

            if (variant is null && product.Variants.Any(x => x.IsActive))
            {
                throw new CommerceRuleException("Insufficient stock.");
            }
        }
        else
        {
            variant = product.Variants.SingleOrDefault(x => x.Id == variantId && x.IsActive)
                ?? throw new CommerceRuleException("Product variant is unavailable.");
        }

        var selectedVariantId = variant?.Id;
        var stock = variant?.StockQuantity ?? product.StockQuantity;
        if (quantity > stock) throw new CommerceRuleException("Insufficient stock.");

        var cart = await GetOrCreateAsync(owner, cancellationToken);
        var item = cart.Items.SingleOrDefault(x => x.ProductId == productId && x.ProductVariantId == selectedVariantId);
        if (item is null)
        {
            item = new CartItem
            {
                CartId = cart.Id,
                Cart = cart,
                ProductId = productId,
                ProductVariantId = selectedVariantId,
                Product = product,
                ProductVariant = variant,
                Quantity = quantity,
                CreatedAtUtc = Now,
                UpdatedAtUtc = Now,
            };
            cart.Items.Add(item);
            dbContext.CartItems.Add(item);
        }
        else
        {
            item.Quantity = Math.Min(stock, checked(item.Quantity + quantity));
            if (item.Quantity > _options.MaximumCartItemQuantity) throw new CommerceRuleException("Maximum cart quantity exceeded.");
            item.UpdatedAtUtc = Now;
        }

        cart.UpdatedAtUtc = Now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return await PriceWithCouponRecoveryAsync(cart, owner.UserId, cancellationToken);
    }

    public async Task<CartSummary> UpdateQuantityAsync(CartOwner owner, Guid itemId, int quantity, CancellationToken cancellationToken)
    {
        if (quantity <= 0) return await RemoveAsync(owner, itemId, cancellationToken);
        if (quantity > _options.MaximumCartItemQuantity) throw new CommerceRuleException("Maximum cart quantity exceeded.");
        var cart = await GetOrCreateAsync(owner, cancellationToken);
        var item = cart.Items.SingleOrDefault(x => x.Id == itemId) ?? throw new CommerceRuleException("Cart item was not found.");
        var stock = item.ProductVariant?.StockQuantity ?? item.Product.StockQuantity;
        if (quantity > stock) throw new CommerceRuleException("Insufficient stock.");
        item.Quantity = quantity;
        item.UpdatedAtUtc = Now;
        cart.UpdatedAtUtc = Now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return await PriceWithCouponRecoveryAsync(cart, owner.UserId, cancellationToken);
    }

    public async Task<CartSummary> RemoveAsync(CartOwner owner, Guid itemId, CancellationToken cancellationToken)
    {
        var cart = await GetOrCreateAsync(owner, cancellationToken);
        var item = cart.Items.SingleOrDefault(x => x.Id == itemId) ?? throw new CommerceRuleException("Cart item was not found.");
        dbContext.CartItems.Remove(item);
        cart.Items.Remove(item);
        cart.UpdatedAtUtc = Now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return await PriceWithCouponRecoveryAsync(cart, owner.UserId, cancellationToken);
    }

    public async Task<CartSummary> ClearAsync(CartOwner owner, CancellationToken cancellationToken)
    {
        var cart = await GetOrCreateAsync(owner, cancellationToken);
        dbContext.CartItems.RemoveRange(cart.Items);
        cart.Items.Clear();
        cart.CouponCode = null;
        cart.UpdatedAtUtc = Now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return await discountEngine.PriceAsync(cart, owner.UserId, cancellationToken);
    }

    public async Task<CartSummary> ApplyCouponAsync(CartOwner owner, string code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new CommerceRuleException("Coupon code is required.");
        var cart = await GetOrCreateAsync(owner, cancellationToken);
        var previousCode = cart.CouponCode;
        cart.CouponCode = code.Trim().ToUpperInvariant();
        CartSummary summary;
        try { summary = await discountEngine.PriceAsync(cart, owner.UserId, cancellationToken); }
        catch
        {
            cart.CouponCode = previousCode;
            throw;
        }
        cart.UpdatedAtUtc = Now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return summary;
    }

    public async Task<CartSummary> RemoveCouponAsync(CartOwner owner, CancellationToken cancellationToken)
    {
        var cart = await GetOrCreateAsync(owner, cancellationToken);
        cart.CouponCode = null;
        cart.UpdatedAtUtc = Now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return await discountEngine.PriceAsync(cart, owner.UserId, cancellationToken);
    }

    public async Task<CartMergeResult> MergeGuestCartAsync(Guid userId, Guid guestToken, CancellationToken cancellationToken)
    {
        var userCart = await GetOrCreateAsync(new CartOwner(userId, null), cancellationToken);
        var guestCart = await LoadCartAsync(new CartOwner(null, guestToken), cancellationToken);
        if (guestCart is null || guestCart.Id == userCart.Id)
            return new CartMergeResult(await PriceWithCouponRecoveryAsync(userCart, userId, cancellationToken), []);

        var warnings = new List<string>();
        foreach (var guestItem in guestCart.Items)
        {
            if (!guestItem.Product.IsActive || guestItem.ProductVariant is { IsActive: false })
            {
                warnings.Add($"{guestItem.Product.Name} is no longer available and was removed from the guest cart.");
                continue;
            }

            var existing = userCart.Items.SingleOrDefault(x => x.ProductId == guestItem.ProductId && x.ProductVariantId == guestItem.ProductVariantId);
            var stock = guestItem.ProductVariant?.StockQuantity ?? guestItem.Product.StockQuantity;
            var desired = guestItem.Quantity + (existing?.Quantity ?? 0);
            var merged = Math.Min(Math.Min(desired, stock), _options.MaximumCartItemQuantity);
            if (merged < desired) warnings.Add($"{guestItem.Product.Name} miktarı mevcut stoğa göre ayarlandı.");
            if (merged <= 0) continue;
            if (existing is null)
            {
                var item = new CartItem
                {
                    CartId = userCart.Id,
                    Cart = userCart,
                    ProductId = guestItem.ProductId,
                    ProductVariantId = guestItem.ProductVariantId,
                    Quantity = merged,
                    Product = guestItem.Product,
                    ProductVariant = guestItem.ProductVariant,
                    CreatedAtUtc = Now,
                    UpdatedAtUtc = Now,
                };
                userCart.Items.Add(item);
                dbContext.CartItems.Add(item);
            }
            else
            {
                existing.Quantity = merged;
                existing.UpdatedAtUtc = Now;
            }
        }

        userCart.CouponCode ??= guestCart.CouponCode;
        var mergedCouponCode = userCart.CouponCode;
        userCart.UpdatedAtUtc = Now;
        dbContext.Carts.Remove(guestCart);
        await dbContext.SaveChangesAsync(cancellationToken);
        var summary = await PriceWithCouponRecoveryAsync(userCart, userId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(mergedCouponCode) && string.IsNullOrWhiteSpace(summary.CouponCode))
            warnings.Add("The guest cart coupon was no longer valid and was removed.");
        return new CartMergeResult(summary, warnings);
    }

    private async Task<CartSummary> PriceWithCouponRecoveryAsync(Cart cart, Guid? userId, CancellationToken cancellationToken)
    {
        try
        {
            return await discountEngine.PriceAsync(cart, userId, cancellationToken);
        }
        catch (CommerceRuleException exception) when (!string.IsNullOrWhiteSpace(cart.CouponCode))
        {
            cart.CouponCode = null;
            cart.UpdatedAtUtc = Now;
            await dbContext.SaveChangesAsync(cancellationToken);
            var summary = await discountEngine.PriceAsync(cart, userId, cancellationToken);
            return summary with { Warnings = summary.Warnings.Append($"Coupon was removed: {exception.Message}").ToList() };
        }
    }

    private async Task<Cart> GetOrCreateAsync(CartOwner owner, CancellationToken cancellationToken)
    {
        if (owner.IsEmpty) throw new CommerceRuleException("A cart owner is required.");
        var cart = await LoadCartAsync(owner, cancellationToken);
        if (cart is not null) return cart;
        cart = new Cart
        {
            UserId = owner.UserId,
            GuestToken = owner.UserId is null ? owner.GuestToken : null,
            ExpiresAtUtc = Now.AddDays(_options.GuestCartLifetimeDays),
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now,
        };
        dbContext.Carts.Add(cart);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return cart;
        }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();
            var concurrentCart = await LoadCartAsync(owner, cancellationToken);
            if (concurrentCart is null) throw;
            return concurrentCart;
        }
    }

    private Task<Cart?> LoadCartAsync(CartOwner owner, CancellationToken cancellationToken) =>
        dbContext.Carts.Include(x => x.Items).ThenInclude(x => x.Product).ThenInclude(x => x.Images)
            .Include(x => x.Items).ThenInclude(x => x.ProductVariant)
            .SingleOrDefaultAsync(x => owner.UserId != null ? x.UserId == owner.UserId : x.UserId == null && x.GuestToken == owner.GuestToken, cancellationToken);

    private DateTimeOffset Now => timeProvider.GetUtcNow();
}
