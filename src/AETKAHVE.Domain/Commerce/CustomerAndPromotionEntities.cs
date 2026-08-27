using AETKAHVE.Domain.Common;

namespace AETKAHVE.Domain.Commerce;

public sealed class Address : SoftDeletableCommerceEntity
{
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string? Neighborhood { get; set; }
    public string? PostalCode { get; set; }
    public string AddressLine { get; set; } = string.Empty;
    public bool IsDefaultShipping { get; set; }
    public bool IsDefaultBilling { get; set; }
}

public sealed class Cart : CommerceEntity, IConcurrencyTracked
{
    public Guid? UserId { get; set; }
    public Guid? GuestToken { get; set; }
    public string? CouponCode { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();
    public List<CartItem> Items { get; set; } = [];
}

public sealed class CartItem : CommerceEntity
{
    public Guid CartId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public int Quantity { get; set; }
    public Cart Cart { get; set; } = null!;
    public Product Product { get; set; } = null!;
    public ProductVariant? ProductVariant { get; set; }
}

public sealed class Favorite : CommerceEntity
{
    public Guid UserId { get; set; }
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
}

public sealed class Campaign : CommerceEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public DiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public decimal? MinimumCartAmount { get; set; }
    public decimal? MaximumDiscountAmount { get; set; }
    public DateTimeOffset StartDateUtc { get; set; }
    public DateTimeOffset EndDateUtc { get; set; }
    public bool IsActive { get; set; } = true;
    public bool CanCombineWithOtherDiscounts { get; set; }
    public List<CampaignProduct> Products { get; set; } = [];
    public List<CampaignCategory> Categories { get; set; } = [];
}

public sealed class CampaignProduct
{
    public Guid CampaignId { get; set; }
    public Guid ProductId { get; set; }
    public Campaign Campaign { get; set; } = null!;
    public Product Product { get; set; } = null!;
}

public sealed class CampaignCategory
{
    public Guid CampaignId { get; set; }
    public Guid CategoryId { get; set; }
    public Campaign Campaign { get; set; } = null!;
    public Category Category { get; set; } = null!;
}

public sealed class Coupon : CommerceEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public DiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public decimal? MinimumCartAmount { get; set; }
    public decimal? MaximumDiscountAmount { get; set; }
    public DateTimeOffset StartDateUtc { get; set; }
    public DateTimeOffset EndDateUtc { get; set; }
    public int? TotalUsageLimit { get; set; }
    public int? PerUserUsageLimit { get; set; }
    public bool IsFirstOrderOnly { get; set; }
    public bool IsActive { get; set; } = true;
    public bool CanCombineWithOtherDiscounts { get; set; }
    public List<CouponUsage> Usages { get; set; } = [];
}

public sealed class CouponUsage : CommerceEntity
{
    public Guid CouponId { get; set; }
    public Guid UserId { get; set; }
    public Guid OrderId { get; set; }
    public CouponUsageStatus Status { get; set; }
    public Coupon Coupon { get; set; } = null!;
    public Order Order { get; set; } = null!;
}
