using AETKAHVE.Domain.Commerce;

namespace AETKAHVE.Application.Commerce;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => TotalCount == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public sealed record ProductQuery(
    string? Search = null,
    Guid? CategoryId = null,
    decimal? MinimumPrice = null,
    decimal? MaximumPrice = null,
    Guid? CoffeeTypeId = null,
    Guid? BeanTypeId = null,
    Guid? RoastLevelId = null,
    Guid? OriginId = null,
    bool InStockOnly = false,
    bool DiscountedOnly = false,
    string Sort = "recommended",
    int Page = 1,
    int PageSize = 24);

public sealed record ProductSummary(
    Guid Id,
    string Name,
    string Slug,
    string ImageUrl,
    string ImageAlt,
    string CategoryName,
    string OriginName,
    string RoastLevelName,
    decimal DisplayPrice,
    decimal? OriginalPrice,
    bool IsDiscounted,
    bool IsInStock,
    bool IsFavorite);

public sealed record ProductVariantDetails(
    Guid Id,
    decimal Weight,
    WeightUnit Unit,
    string Sku,
    decimal DisplayPrice,
    decimal? OriginalPrice,
    int AvailableQuantity);

public sealed record ProductDetails(
    Guid Id,
    string Name,
    string Slug,
    string Sku,
    string ShortDescription,
    string Description,
    string CategoryName,
    string? BrandName,
    string? CoffeeTypeName,
    string? BeanTypeName,
    string? RoastLevelName,
    string? OriginName,
    decimal DisplayPrice,
    decimal? OriginalPrice,
    decimal TaxRate,
    int AvailableQuantity,
    IReadOnlyList<(string Url, string AltText, bool IsPrimary)> Images,
    IReadOnlyList<ProductVariantDetails> Variants,
    decimal AverageRating,
    int ReviewCount,
    bool IsFavorite);

public readonly record struct CartOwner(Guid? UserId, Guid? GuestToken)
{
    public bool IsEmpty => UserId is null && GuestToken is null;
}

public sealed record CartLine(
    Guid ItemId,
    Guid ProductId,
    Guid? VariantId,
    string ProductName,
    string? VariantName,
    string Sku,
    int Quantity,
    int AvailableQuantity,
    decimal UnitPrice,
    decimal LineSubtotal,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal LineTotal,
    string ImageUrl);

public sealed record CartSummary(
    Guid CartId,
    IReadOnlyList<CartLine> Items,
    decimal Subtotal,
    decimal DiscountTotal,
    decimal TaxTotal,
    decimal ShippingTotal,
    decimal GrandTotal,
    string Currency,
    string? CouponCode,
    IReadOnlyList<string> Warnings)
{
    public int ItemCount => Items.Sum(x => x.Quantity);
}

public sealed record CartMergeResult(CartSummary Cart, IReadOnlyList<string> Warnings);

public sealed record AddressInput(
    string Title,
    string FirstName,
    string LastName,
    string PhoneNumber,
    string Country,
    string City,
    string District,
    string? Neighborhood,
    string? PostalCode,
    string AddressLine,
    bool IsDefaultShipping,
    bool IsDefaultBilling);

public sealed record AddressDetails(
    Guid Id,
    string Title,
    string FirstName,
    string LastName,
    string PhoneNumber,
    string Country,
    string City,
    string District,
    string? Neighborhood,
    string? PostalCode,
    string AddressLine,
    bool IsDefaultShipping,
    bool IsDefaultBilling);

public sealed record CheckoutRequest(
    Guid UserId,
    Guid CartId,
    Guid ShippingAddressId,
    Guid BillingAddressId,
    string IdempotencyKey,
    string? CustomerNote,
    string PaymentScenario = "success");

public sealed record CheckoutInitializationResult(
    Guid OrderId,
    string OrderNumber,
    Guid PaymentId,
    string Provider,
    string RequestReference,
    decimal Amount,
    string Currency,
    string CallbackUrl);

public sealed record CheckoutCompletionResult(
    Guid OrderId,
    string OrderNumber,
    OrderStatus OrderStatus,
    PaymentStatus PaymentStatus,
    bool IsIdempotentReplay,
    string Message);

public sealed record PaymentRequest(
    Guid PaymentId,
    Guid OrderId,
    decimal Amount,
    string Currency,
    string IdempotencyKey,
    string Scenario,
    string CallbackUrl);

public sealed record PaymentInitializationResult(
    bool Succeeded,
    string RequestReference,
    string? RedirectUrl,
    string? FailureCode,
    string? FailureReason);

public sealed record PaymentCallbackRequest(string RequestReference, string TransactionId, string StatusCode);

public sealed record PaymentVerificationResult(
    bool Succeeded,
    bool Cancelled,
    string TransactionId,
    decimal Amount,
    string Currency,
    string ResponseCode,
    string? FailureReason);

public sealed record RefundRequest(Guid PaymentId, string TransactionId, decimal Amount, string Currency, string IdempotencyKey);
public sealed record RefundResult(bool Succeeded, string? ProviderReference, string? FailureReason);

public sealed record ShipmentCreateRequest(Guid OrderId, string OrderNumber, string ShippingAddressSnapshot);
public sealed record ShipmentCreateResult(bool Succeeded, string? TrackingNumber, string? TrackingUrl, string? FailureReason);
public sealed record ShipmentTrackingResult(bool Succeeded, ShipmentStatus Status, string? Description);
public sealed record ShipmentCancelResult(bool Succeeded, string? FailureReason);

public sealed record InvoiceLine(string Name, string Sku, int Quantity, decimal UnitPrice, decimal Discount, decimal Tax, decimal Total);
public sealed record InvoiceDocument(
    string InvoiceNumber,
    DateTimeOffset InvoiceDateUtc,
    string BrandName,
    string OrderNumber,
    string CustomerName,
    string BillingAddress,
    IReadOnlyList<InvoiceLine> Lines,
    decimal Subtotal,
    decimal DiscountTotal,
    decimal TaxTotal,
    decimal ShippingTotal,
    decimal GrandTotal,
    string Currency);

public sealed record StoredFile(string StorageKey, string ContentType, long Length);
public sealed record EmailMessage(string Destination, string Subject, string HtmlBody);
public sealed record SmsMessage(string Destination, string Body);
public sealed record DeliveryResult(bool Succeeded, string? ProviderReference, string? FailureReason);

public sealed record OrderSummary(Guid Id, string OrderNumber, OrderStatus Status, PaymentStatus PaymentStatus, decimal GrandTotal, string Currency, DateTimeOffset CreatedAtUtc);
public sealed record OrderLineDetails(Guid OrderItemId, string Name, string Sku, int Quantity, decimal UnitPrice, decimal Discount, decimal Tax, decimal Total);
public sealed record OrderDetails(OrderSummary Summary, IReadOnlyList<OrderLineDetails> Items, ShipmentStatus ShippingStatus, string ShippingAddress, string BillingAddress);
public sealed record InvoiceFile(Stream Content, string FileName);
public sealed record InvoiceSummary(Guid Id, string InvoiceNumber, string OrderNumber, decimal GrandTotal, string Currency, DateTimeOffset InvoiceDateUtc);

public sealed record ReturnItemInput(Guid OrderItemId, int Quantity, string Reason, ReturnItemCondition Condition, string? ImageStorageKey);
public sealed record ReturnCreateRequest(Guid UserId, Guid OrderId, string Reason, string? Description, IReadOnlyList<ReturnItemInput> Items);
public sealed record ReturnDecision(Guid ReturnRequestId, Guid AdminUserId, ReturnStatus NewStatus, string Response, bool Restock);
public sealed record ReturnSummary(Guid Id, Guid OrderId, string OrderNumber, ReturnStatus Status, decimal RefundAmount, DateTimeOffset RequestedAtUtc);

public sealed record ReviewInput(Guid UserId, Guid OrderItemId, int Rating, string Comment);
public sealed record ReviewSummary(Guid Id, string ProductName, int Rating, string Comment, ReviewStatus Status, DateTimeOffset CreatedAtUtc);
public sealed record ReportFilter(DateTimeOffset FromUtc, DateTimeOffset ToUtc, int Page = 1, int PageSize = 50);
public sealed record SalesReport(decimal GrossRevenue, decimal DiscountTotal, decimal TaxTotal, decimal ShippingRevenue, decimal RefundTotal, decimal NetRevenue, int OrderCount, decimal AverageOrderValue);

public sealed record CatalogLookupItem(Guid Id, string Name, string Slug);
public sealed record CatalogLookupSet(
    IReadOnlyList<CatalogLookupItem> Categories,
    IReadOnlyList<CatalogLookupItem> Brands,
    IReadOnlyList<CatalogLookupItem> CoffeeTypes,
    IReadOnlyList<CatalogLookupItem> BeanTypes,
    IReadOnlyList<CatalogLookupItem> RoastLevels,
    IReadOnlyList<CatalogLookupItem> Origins);
public sealed record CampaignSummary(Guid Id, string Name, string Slug, DiscountType DiscountType, decimal DiscountValue, DateTimeOffset EndDateUtc);
public sealed record NotificationItem(Guid Id, string Title, string Message, string Type, string? RelatedEntityType, Guid? RelatedEntityId, bool IsRead, DateTimeOffset CreatedAtUtc);

public sealed record AdminProductInput(
    Guid? Id,
    string Name,
    string Slug,
    string Sku,
    string ShortDescription,
    string Description,
    decimal BasePrice,
    decimal? DiscountedPrice,
    decimal TaxRate,
    int StockQuantity,
    int CriticalStockLevel,
    Guid CategoryId,
    Guid? BrandId,
    Guid? CoffeeTypeId,
    Guid? BeanTypeId,
    Guid? RoastLevelId,
    Guid? OriginId,
    bool IsActive,
    bool IsFeatured);

public sealed record AdminCatalogLookupInput(string Kind, Guid? Id, string Name, string Slug, bool IsActive, string? CountryCode = null);

public sealed record AdminCampaignInput(Guid? Id, string Name, string Slug, DiscountType DiscountType, decimal DiscountValue, decimal? MinimumCartAmount, decimal? MaximumDiscountAmount, DateTimeOffset StartDateUtc, DateTimeOffset EndDateUtc, bool IsActive, bool CanCombineWithOtherDiscounts, IReadOnlyList<Guid>? ProductIds = null, IReadOnlyList<Guid>? CategoryIds = null);
public sealed record AdminCouponInput(Guid? Id, string Name, string Code, DiscountType DiscountType, decimal DiscountValue, decimal? MinimumCartAmount, decimal? MaximumDiscountAmount, DateTimeOffset StartDateUtc, DateTimeOffset EndDateUtc, int? TotalUsageLimit, int? PerUserUsageLimit, bool IsFirstOrderOnly, bool IsActive, bool CanCombineWithOtherDiscounts);
public sealed record AdminShipmentInput(Guid OrderId, string? Note, DateTimeOffset? EstimatedDeliveryDateUtc);
public sealed record AdminInvoiceSummary(Guid Id, string InvoiceNumber, string OrderNumber, decimal GrandTotal, string Currency, DateTimeOffset InvoiceDateUtc);
public sealed record AdminShipmentSummary(Guid Id, Guid OrderId, string OrderNumber, ShipmentStatus Status, string? TrackingNumber, DateTimeOffset UpdatedAtUtc);
public sealed record AdminCampaignSummary(Guid Id, string Name, string Slug, DiscountType DiscountType, decimal DiscountValue, bool IsActive, DateTimeOffset StartDateUtc, DateTimeOffset EndDateUtc);
public sealed record AdminCouponSummary(Guid Id, string Name, string Code, DiscountType DiscountType, decimal DiscountValue, bool IsActive, int ConsumedCount, int? TotalUsageLimit);
public sealed record AdminReturnSummary(Guid Id, string OrderNumber, Guid UserId, ReturnStatus Status, decimal RefundAmount, DateTimeOffset RequestedAtUtc);
public sealed record AdminReviewSummary(Guid Id, string ProductName, Guid UserId, int Rating, ReviewStatus Status, DateTimeOffset CreatedAtUtc);
public sealed record AdminContactMessageSummary(Guid Id, string FullName, string Email, string Subject, ContactMessageStatus Status, DateTimeOffset CreatedAtUtc);

public sealed record ServiceResult(bool Succeeded, string Message)
{
    public static ServiceResult Success(string message) => new(true, message);
    public static ServiceResult Failure(string message) => new(false, message);
}
