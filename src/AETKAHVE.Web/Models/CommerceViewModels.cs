using System.ComponentModel.DataAnnotations;
using AETKAHVE.Application.Commerce;
using AETKAHVE.Domain.Commerce;

namespace AETKAHVE.Web.Models;

public sealed record ProductListViewModel(PagedResult<ProductSummary> Products, CatalogLookupSet Lookups, ProductQuery Query);
public sealed record ProductDetailPageViewModel(ProductDetails Product);
public sealed record CampaignListViewModel(IReadOnlyList<CampaignSummary> Campaigns);
public sealed record CategoryListViewModel(IReadOnlyList<CollectionCardViewModel> Categories);

// CatalogLookupItem'ın (Id/Name/Slug) sunum katmanı sarmalayıcısı — ImageUrl/Description/
// ProductCount bugün backend'de yok (bkz. docs/contracts/requests/claude-design-collections-image.md);
// null bırakılıyor, uydurulmuyor. Alanlar gerçek veriyle dolduğunda view otomatik devreye girer.
public sealed record CollectionCardViewModel(
    Guid Id,
    string Name,
    string Slug,
    string? ImageUrl = null,
    string? Description = null,
    int? ProductCount = null);
public sealed record CartPageViewModel(CartSummary Cart);
public sealed record FavoritePageViewModel(PagedResult<ProductSummary> Products);
public sealed record CheckoutPageViewModel(CartSummary Cart, IReadOnlyList<AddressDetails> Addresses, string IdempotencyKey);
public sealed record OrderListViewModel(PagedResult<OrderSummary> Orders);
public sealed record OrderDetailViewModel(OrderDetails Order);
public sealed record AddressListViewModel(IReadOnlyList<AddressDetails> Addresses);
public sealed record InvoiceListViewModel(PagedResult<InvoiceSummary> Invoices);
public sealed record ReturnListViewModel(PagedResult<ReturnSummary> Returns);
public sealed record ReviewListViewModel(PagedResult<ReviewSummary> Reviews);
public sealed record AdminCatalogViewModel(CatalogLookupSet Lookups);
public sealed record AdminInvoiceListViewModel(PagedResult<AdminInvoiceSummary> Invoices);
public sealed record AdminShipmentListViewModel(PagedResult<AdminShipmentSummary> Shipments);
public sealed record AdminCampaignListViewModel(PagedResult<AdminCampaignSummary> Campaigns);
public sealed record AdminCouponListViewModel(PagedResult<AdminCouponSummary> Coupons);
public sealed record AdminReturnListViewModel(PagedResult<AdminReturnSummary> Returns);
public sealed record AdminReviewListViewModel(PagedResult<AdminReviewSummary> Reviews);
public sealed record AdminMessageListViewModel(PagedResult<AdminContactMessageSummary> Messages);
public sealed record AdminDashboardViewModel(string Title, string PortalLabel, AdminDashboardSummary Summary);
public sealed record ManagementNavigationViewModel(string PortalLabel, string DashboardPath, string SecurityPath);
public sealed record CommerceMutationResponse(bool Success, string Message, int? CartItemCount = null, decimal? Subtotal = null, decimal? GrandTotal = null, object? Data = null);

public sealed class AddCartItemInput
{
    public Guid ProductId { get; set; }
    public Guid? VariantId { get; set; }
    [Range(1, 100)] public int Quantity { get; set; } = 1;
}

public sealed class UpdateCartQuantityInput
{
    [Range(0, 100)] public int Quantity { get; set; }
}

public sealed class CouponInput
{
    [Required, StringLength(80)] public string Code { get; set; } = string.Empty;
}

public sealed class CheckoutInput
{
    public Guid CartId { get; set; }
    public Guid ShippingAddressId { get; set; }
    public Guid BillingAddressId { get; set; }
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [StringLength(1000)] public string? CustomerNote { get; set; }
    [StringLength(30)] public string PaymentScenario { get; set; } = "success";
}

public sealed class AddressInputModel
{
    public Guid? Id { get; set; }
    [Required, StringLength(80)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(100)] public string FirstName { get; set; } = string.Empty;
    [Required, StringLength(100)] public string LastName { get; set; } = string.Empty;
    [Required, StringLength(30)] public string PhoneNumber { get; set; } = string.Empty;
    [Required, StringLength(100)] public string Country { get; set; } = string.Empty;
    [Required, StringLength(100)] public string City { get; set; } = string.Empty;
    [Required, StringLength(100)] public string District { get; set; } = string.Empty;
    [StringLength(150)] public string? Neighborhood { get; set; }
    [StringLength(20)] public string? PostalCode { get; set; }
    [Required, StringLength(1000)] public string AddressLine { get; set; } = string.Empty;
    public bool IsDefaultShipping { get; set; }
    public bool IsDefaultBilling { get; set; }
}

public sealed class ReturnInputModel
{
    public Guid OrderId { get; set; }
    [Required, StringLength(250)] public string Reason { get; set; } = string.Empty;
    [StringLength(2000)] public string? Description { get; set; }
    [MinLength(1)] public List<ReturnLineInputModel> Items { get; set; } = [];
}

public sealed class ReturnLineInputModel
{
    public Guid OrderItemId { get; set; }
    [Range(1, 100)] public int Quantity { get; set; }
    [Required, StringLength(500)] public string Reason { get; set; } = string.Empty;
    public ReturnItemCondition Condition { get; set; }
    [StringLength(512)] public string? ImageStorageKey { get; set; }
}

public sealed class ReviewInputModel
{
    public Guid OrderItemId { get; set; }
    [Range(1, 5)] public int Rating { get; set; }
    [Required, StringLength(3000)] public string Comment { get; set; } = string.Empty;
}

public sealed class ContactInputModel
{
    [Required, StringLength(200)] public string FullName { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(320)] public string Email { get; set; } = string.Empty;
    [StringLength(30)] public string? PhoneNumber { get; set; }
    [Required, StringLength(200)] public string Subject { get; set; } = string.Empty;
    [Required, StringLength(5000)] public string Message { get; set; } = string.Empty;
    [Range(typeof(bool), "true", "true")] public bool PrivacyAccepted { get; set; }
}
