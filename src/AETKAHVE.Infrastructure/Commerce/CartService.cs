using AETKAHVE.Application.Commerce;
using AETKAHVE.Domain.Commerce;
using AETKAHVE.Domain.Common;
using AETKAHVE.Infrastructure.Options;
using AETKAHVE.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AETKAHVE.Infrastructure.Commerce;

public sealed class CartService(
    AppDbContext dbContext,
    IDiscountEngine discountEngine,
    IOptions<CommerceOptions> options,
    TimeProvider timeProvider) : ICartService
{
    private const int MaximumPersistenceAttempts = 4;
    private readonly CommerceOptions _options = options.Value;

    public Task<CartSummary> GetAsync(CartOwner owner, CancellationToken cancellationToken) =>
        ExecuteWithRetryAsync(async () =>
        {
            var warnings = new List<string>();
            var cart = await GetOrCreateAsync(owner, warnings, cancellationToken);
            var summary = await PriceWithCouponRecoveryAsync(cart, owner.UserId, warnings, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return summary;
        }, cancellationToken);

    public Task<CartSummary> AddAsync(
        CartOwner owner,
        Guid productId,
        Guid? variantId,
        int quantity,
        CancellationToken cancellationToken)
    {
        if (quantity < 1 || quantity > _options.MaximumCartItemQuantity)
            throw new CommerceRuleException("Cart quantity is invalid.");

        return ExecuteWithRetryAsync(async () =>
        {
            var warnings = new List<string>();
            var product = await dbContext.Products
                .Include(x => x.Images)
                .Include(x => x.Variants)
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
                    throw new CommerceRuleException("Insufficient stock.");
            }
            else
            {
                variant = product.Variants.SingleOrDefault(x => x.Id == variantId && x.IsActive)
                    ?? throw new CommerceRuleException("Product variant is unavailable.");
            }

            var selectedVariantId = variant?.Id;
            var stock = variant?.StockQuantity ?? product.StockQuantity;
            if (quantity > stock) throw new CommerceRuleException("Insufficient stock.");

            var cart = await GetOrCreateAsync(owner, warnings, cancellationToken);
            var item = cart.Items.SingleOrDefault(x =>
                x.ProductId == productId && x.ProductVariantId == selectedVariantId);
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
                var desired = (long)item.Quantity + quantity;
                if (desired > _options.MaximumCartItemQuantity)
                    throw new CommerceRuleException("Maximum cart quantity exceeded.");

                item.Quantity = (int)Math.Min(desired, stock);
                if (item.Quantity < desired)
                    warnings.Add($"{product.Name} miktarı mevcut stoğa göre ayarlandı.");
                item.UpdatedAtUtc = Now;
            }

            Touch(cart);
            var summary = await PriceWithCouponRecoveryAsync(cart, owner.UserId, warnings, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return summary;
        }, cancellationToken);
    }

    public Task<CartSummary> UpdateQuantityAsync(
        CartOwner owner,
        Guid itemId,
        int quantity,
        CancellationToken cancellationToken)
    {
        if (quantity <= 0) return RemoveAsync(owner, itemId, cancellationToken);
        if (quantity > _options.MaximumCartItemQuantity)
            throw new CommerceRuleException("Maximum cart quantity exceeded.");

        return ExecuteWithRetryAsync(async () =>
        {
            var warnings = new List<string>();
            var cart = await GetOrCreateAsync(owner, warnings, cancellationToken);
            var item = cart.Items.SingleOrDefault(x => x.Id == itemId)
                ?? throw new CommerceRuleException("Cart item was not found.");
            var stock = item.ProductVariant?.StockQuantity ?? item.Product.StockQuantity;
            if (quantity > stock) throw new CommerceRuleException("Insufficient stock.");

            item.Quantity = quantity;
            item.UpdatedAtUtc = Now;
            Touch(cart);
            var summary = await PriceWithCouponRecoveryAsync(cart, owner.UserId, warnings, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return summary;
        }, cancellationToken);
    }

    public Task<CartSummary> RemoveAsync(CartOwner owner, Guid itemId, CancellationToken cancellationToken) =>
        ExecuteWithRetryAsync(async () =>
        {
            var warnings = new List<string>();
            var cart = await GetOrCreateAsync(owner, warnings, cancellationToken);
            var item = cart.Items.SingleOrDefault(x => x.Id == itemId)
                ?? throw new CommerceRuleException("Cart item was not found.");
            RemoveItem(cart, item);
            Touch(cart);
            var summary = await PriceWithCouponRecoveryAsync(cart, owner.UserId, warnings, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return summary;
        }, cancellationToken);

    public Task<CartSummary> ClearAsync(CartOwner owner, CancellationToken cancellationToken) =>
        ExecuteWithRetryAsync(async () =>
        {
            var warnings = new List<string>();
            var cart = await GetOrCreateAsync(owner, warnings, cancellationToken);
            foreach (var item in cart.Items.ToList()) RemoveItem(cart, item);
            cart.CouponCode = null;
            Touch(cart);
            var summary = await discountEngine.PriceAsync(cart, owner.UserId, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return AppendWarnings(summary, warnings);
        }, cancellationToken);

    public Task<CartSummary> ApplyCouponAsync(
        CartOwner owner,
        string code,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new CommerceRuleException("Coupon code is required.");

        return ExecuteWithRetryAsync(async () =>
        {
            var warnings = new List<string>();
            var cart = await GetOrCreateAsync(owner, warnings, cancellationToken);
            var previousCode = cart.CouponCode;
            cart.CouponCode = code.Trim().ToUpperInvariant();
            CartSummary summary;
            try
            {
                summary = await discountEngine.PriceAsync(cart, owner.UserId, cancellationToken);
            }
            catch
            {
                cart.CouponCode = previousCode;
                throw;
            }

            Touch(cart);
            await dbContext.SaveChangesAsync(cancellationToken);
            return AppendWarnings(summary, warnings);
        }, cancellationToken);
    }

    public Task<CartSummary> RemoveCouponAsync(CartOwner owner, CancellationToken cancellationToken) =>
        ExecuteWithRetryAsync(async () =>
        {
            var warnings = new List<string>();
            var cart = await GetOrCreateAsync(owner, warnings, cancellationToken);
            cart.CouponCode = null;
            Touch(cart);
            var summary = await discountEngine.PriceAsync(cart, owner.UserId, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return AppendWarnings(summary, warnings);
        }, cancellationToken);

    public Task<CartMergeResult> MergeGuestCartAsync(
        Guid userId,
        Guid guestToken,
        CancellationToken cancellationToken) =>
        ExecuteWithRetryAsync(async () =>
        {
            var warnings = new List<string>();
            var userCart = await GetOrCreateAsync(new CartOwner(userId, null), warnings, cancellationToken);
            var guestCart = await LoadCartAsync(new CartOwner(null, guestToken), cancellationToken);
            if (guestCart is null || guestCart.Id == userCart.Id)
            {
                var unchanged = await PriceWithCouponRecoveryAsync(userCart, userId, warnings, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
                return new CartMergeResult(unchanged, warnings);
            }

            if (IsExpiredGuestCart(guestCart))
            {
                if (guestCart.Items.Count > 0 || !string.IsNullOrWhiteSpace(guestCart.CouponCode))
                    warnings.Add("Süresi dolan misafir sepeti hesaba aktarılmadı.");
                dbContext.Carts.Remove(guestCart);
                var unchanged = await PriceWithCouponRecoveryAsync(userCart, userId, warnings, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
                return new CartMergeResult(unchanged, warnings);
            }

            PrepareCart(guestCart, isGuest: true, warnings);
            foreach (var guestItem in guestCart.Items.ToList())
            {
                var existing = userCart.Items.SingleOrDefault(x =>
                    x.ProductId == guestItem.ProductId &&
                    x.ProductVariantId == guestItem.ProductVariantId);
                var stock = guestItem.ProductVariant?.StockQuantity ?? guestItem.Product.StockQuantity;
                var desired = (long)guestItem.Quantity + (existing?.Quantity ?? 0);
                var merged = (int)Math.Min(desired, Math.Min(stock, _options.MaximumCartItemQuantity));
                if (merged < desired)
                    warnings.Add($"{guestItem.Product.Name} miktarı mevcut stok ve sepet sınırına göre ayarlandı.");
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

            var adoptedGuestCoupon = string.IsNullOrWhiteSpace(userCart.CouponCode) &&
                                     !string.IsNullOrWhiteSpace(guestCart.CouponCode);
            if (adoptedGuestCoupon) userCart.CouponCode = guestCart.CouponCode;
            Touch(userCart);
            dbContext.Carts.Remove(guestCart);

            var summary = await PriceWithCouponRecoveryAsync(userCart, userId, warnings, cancellationToken);
            if (adoptedGuestCoupon && string.IsNullOrWhiteSpace(summary.CouponCode))
                warnings.Add("The guest cart coupon was no longer valid and was removed.");
            await dbContext.SaveChangesAsync(cancellationToken);
            return new CartMergeResult(AppendWarnings(summary, warnings), warnings);
        }, cancellationToken);

    private async Task<CartSummary> PriceWithCouponRecoveryAsync(
        Cart cart,
        Guid? userId,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        CartSummary summary;
        try
        {
            summary = await discountEngine.PriceAsync(cart, userId, cancellationToken);
        }
        catch (CommerceRuleException exception) when (!string.IsNullOrWhiteSpace(cart.CouponCode))
        {
            cart.CouponCode = null;
            Touch(cart);
            warnings.Add($"Coupon was removed: {exception.Message}");
            summary = await discountEngine.PriceAsync(cart, userId, cancellationToken);
        }

        return AppendWarnings(summary, warnings);
    }

    private async Task<Cart> GetOrCreateAsync(
        CartOwner owner,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        if (owner.IsEmpty) throw new CommerceRuleException("A cart owner is required.");
        var cart = await LoadCartAsync(owner, cancellationToken);
        if (cart is null)
        {
            cart = new Cart
            {
                UserId = owner.UserId,
                GuestToken = owner.UserId is null ? owner.GuestToken : null,
                ExpiresAtUtc = Now.AddDays(_options.GuestCartLifetimeDays),
                CreatedAtUtc = Now,
                UpdatedAtUtc = Now,
            };
            dbContext.Carts.Add(cart);
        }

        var housekeepingWasApplied = PrepareCart(cart, owner.UserId is null, warnings);
        if (housekeepingWasApplied && dbContext.Entry(cart).State != EntityState.Added)
            await dbContext.SaveChangesAsync(cancellationToken);
        return cart;
    }

    private bool PrepareCart(Cart cart, bool isGuest, List<string> warnings)
    {
        if (isGuest && IsExpiredGuestCart(cart))
        {
            var hadContents = cart.Items.Count > 0 || !string.IsNullOrWhiteSpace(cart.CouponCode);
            foreach (var item in cart.Items.ToList()) RemoveItem(cart, item);
            cart.CouponCode = null;
            if (hadContents) warnings.Add("Misafir sepetinin süresi dolduğu için sepet yenilendi.");
            SlideGuestExpiration(cart);
            return true;
        }

        var normalized = NormalizeCart(cart, warnings);
        if (isGuest) SlideGuestExpiration(cart);
        return normalized;
    }

    private bool NormalizeCart(Cart cart, List<string> warnings)
    {
        var changed = false;
        foreach (var item in cart.Items.ToList())
        {
            var productUnavailable = item.Product.DeletedAtUtc is not null || !item.Product.IsActive;
            var variantUnavailable = item.ProductVariantId is not null &&
                                     (item.ProductVariant is null ||
                                      item.ProductVariant.ProductId != item.ProductId ||
                                      item.ProductVariant.DeletedAtUtc is not null ||
                                      !item.ProductVariant.IsActive);
            if (productUnavailable || variantUnavailable)
            {
                warnings.Add($"{item.Product.Name} artık satışta olmadığı için sepetten çıkarıldı.");
                RemoveItem(cart, item);
                changed = true;
                continue;
            }

            var stock = item.ProductVariant?.StockQuantity ?? item.Product.StockQuantity;
            var maximumAllowed = Math.Min(stock, _options.MaximumCartItemQuantity);
            if (maximumAllowed <= 0 || item.Quantity <= 0)
            {
                warnings.Add($"{item.Product.Name} stokta olmadığı için sepetten çıkarıldı.");
                RemoveItem(cart, item);
                changed = true;
                continue;
            }

            if (item.Quantity > maximumAllowed)
            {
                item.Quantity = maximumAllowed;
                item.UpdatedAtUtc = Now;
                warnings.Add($"{item.Product.Name} miktarı mevcut stok ve sepet sınırına göre ayarlandı.");
                changed = true;
            }
        }

        if (changed) Touch(cart);
        return changed;
    }

    private void RemoveItem(Cart cart, CartItem item)
    {
        dbContext.CartItems.Remove(item);
        cart.Items.Remove(item);
    }

    private void SlideGuestExpiration(Cart cart)
    {
        cart.ExpiresAtUtc = Now.AddDays(_options.GuestCartLifetimeDays);
        Touch(cart);
    }

    private bool IsExpiredGuestCart(Cart cart) => cart.ExpiresAtUtc <= Now;

    private void Touch(Cart cart)
    {
        cart.UpdatedAtUtc = Now;
        cart.ConcurrencyToken = Guid.NewGuid();
    }

    private Task<Cart?> LoadCartAsync(CartOwner owner, CancellationToken cancellationToken) =>
        dbContext.Carts
            .IgnoreQueryFilters()
            .AsSplitQuery()
            .Include(x => x.Items).ThenInclude(x => x.Product).ThenInclude(x => x.Images)
            .Include(x => x.Items).ThenInclude(x => x.ProductVariant)
            .SingleOrDefaultAsync(
                x => owner.UserId != null
                    ? x.UserId == owner.UserId
                    : x.UserId == null && x.GuestToken == owner.GuestToken,
                cancellationToken);

    private async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaximumPersistenceAttempts; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (DbUpdateException exception) when (IsRetryablePersistenceFailure(exception))
            {
                await PrepareRetryAsync(attempt, cancellationToken);
            }
            catch (SqliteException exception) when (exception.SqliteErrorCode is 5 or 6)
            {
                await PrepareRetryAsync(attempt, cancellationToken);
            }
            catch (SqlException exception) when (exception.Number == 1205)
            {
                await PrepareRetryAsync(attempt, cancellationToken);
            }
        }

        throw new InvalidOperationException("Cart persistence retry loop terminated unexpectedly.");
    }

    private static bool IsRetryablePersistenceFailure(DbUpdateException exception)
    {
        if (exception is DbUpdateConcurrencyException) return true;

        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqliteException { SqliteErrorCode: 5 or 6 }) return true;
            if (current is SqliteException { SqliteExtendedErrorCode: 1555 or 2067 }) return true;
            if (current is SqlException { Number: 1205 or 2601 or 2627 }) return true;
        }

        return false;
    }

    private async Task PrepareRetryAsync(int attempt, CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        if (attempt == MaximumPersistenceAttempts)
            throw new CommerceRuleException("Cart changed concurrently. Please retry the operation.");
        await Task.Delay(TimeSpan.FromMilliseconds(10 * attempt), cancellationToken);
    }

    private static CartSummary AppendWarnings(CartSummary summary, IEnumerable<string> warnings)
    {
        var combined = summary.Warnings
            .Concat(warnings)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return summary with { Warnings = combined };
    }

    private DateTimeOffset Now => timeProvider.GetUtcNow();
}
