using AETKAHVE.Domain.Commerce;

namespace AETKAHVE.Application.Commerce;

public interface ICatalogQueryService
{
    Task<PagedResult<ProductSummary>> SearchAsync(ProductQuery query, Guid? userId, CancellationToken cancellationToken);
    Task<ProductDetails?> GetBySlugAsync(string slug, Guid? userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProductSummary>> GetFeaturedAsync(int count, Guid? userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CatalogLookupItem>> GetCategoriesAsync(CancellationToken cancellationToken);
    Task<CatalogLookupSet> GetLookupSetAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<CampaignSummary>> GetActiveCampaignsAsync(CancellationToken cancellationToken);
}

public interface ICartService
{
    Task<CartSummary> GetAsync(CartOwner owner, CancellationToken cancellationToken);
    Task<CartSummary> AddAsync(CartOwner owner, Guid productId, Guid? variantId, int quantity, CancellationToken cancellationToken);
    Task<CartSummary> UpdateQuantityAsync(CartOwner owner, Guid itemId, int quantity, CancellationToken cancellationToken);
    Task<CartSummary> RemoveAsync(CartOwner owner, Guid itemId, CancellationToken cancellationToken);
    Task<CartSummary> ClearAsync(CartOwner owner, CancellationToken cancellationToken);
    Task<CartSummary> ApplyCouponAsync(CartOwner owner, string code, CancellationToken cancellationToken);
    Task<CartSummary> RemoveCouponAsync(CartOwner owner, CancellationToken cancellationToken);
    Task<CartMergeResult> MergeGuestCartAsync(Guid userId, Guid guestToken, CancellationToken cancellationToken);
}

public interface IFavoriteService
{
    Task<bool> ToggleAsync(Guid userId, Guid productId, CancellationToken cancellationToken);
    Task<PagedResult<ProductSummary>> GetAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken);
}

public interface IDiscountEngine
{
    Task<CartSummary> PriceAsync(Cart cart, Guid? userId, CancellationToken cancellationToken);
}

public interface IAddressService
{
    Task<IReadOnlyList<AddressDetails>> GetAsync(Guid userId, CancellationToken cancellationToken);
    Task<AddressDetails> SaveAsync(Guid userId, Guid? addressId, AddressInput input, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(Guid userId, Guid addressId, CancellationToken cancellationToken);
}

public interface ICheckoutService
{
    Task<CheckoutInitializationResult> InitializeAsync(CheckoutRequest request, string callbackUrl, CancellationToken cancellationToken);
    Task<CheckoutCompletionResult> CompleteAsync(string provider, PaymentCallbackRequest request, CancellationToken cancellationToken);
}

public interface IOrderService
{
    Task<PagedResult<OrderSummary>> GetForUserAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken);
    Task<OrderDetails?> GetForUserAsync(Guid userId, Guid orderId, CancellationToken cancellationToken);
    Task<PagedResult<InvoiceSummary>> GetInvoicesForUserAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken);
    Task<ServiceResult> CancelAsync(Guid userId, Guid orderId, CancellationToken cancellationToken);
    Task<InvoiceFile?> OpenInvoiceAsync(Guid userId, Guid invoiceId, CancellationToken cancellationToken);
}

public interface IInventoryService
{
    Task DeductForOrderAsync(Order order, Guid? actorUserId, CancellationToken cancellationToken);
    Task RestoreForOrderAsync(Order order, StockMovementType movementType, Guid? actorUserId, CancellationToken cancellationToken);
}

public interface IReturnService
{
    Task<PagedResult<ReturnSummary>> GetForUserAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken);
    Task<Guid> CreateAsync(ReturnCreateRequest request, CancellationToken cancellationToken);
    Task<ServiceResult> DecideAsync(ReturnDecision decision, CancellationToken cancellationToken);
}

public interface IReviewService
{
    Task<PagedResult<ReviewSummary>> GetForUserAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken);
    Task<Guid> CreateOrUpdateAsync(ReviewInput input, CancellationToken cancellationToken);
    Task<ServiceResult> DeleteAsync(Guid userId, Guid reviewId, CancellationToken cancellationToken);
}

public interface IReportingService
{
    Task<SalesReport> GetSalesAsync(ReportFilter filter, CancellationToken cancellationToken);
    Task<byte[]> ExportSalesCsvAsync(ReportFilter filter, CancellationToken cancellationToken);
}

public interface IContactService
{
    Task<Guid> SubmitAsync(string fullName, string email, string? phone, string subject, string message, bool privacyAccepted, CancellationToken cancellationToken);
}

public interface INotificationService
{
    Task<IReadOnlyList<NotificationItem>> GetAsync(Guid userId, CancellationToken cancellationToken);
    Task<ServiceResult> MarkReadAsync(Guid userId, Guid? notificationId, CancellationToken cancellationToken);
}

public interface IAdminCommerceService
{
    Task<Guid> SaveProductAsync(Guid adminUserId, AdminProductInput input, CancellationToken cancellationToken);
    Task<Guid> SaveCatalogLookupAsync(Guid adminUserId, AdminCatalogLookupInput input, CancellationToken cancellationToken);
    Task<ServiceResult> AdjustStockAsync(Guid adminUserId, Guid productId, Guid? variantId, int delta, CancellationToken cancellationToken);
    Task<PagedResult<OrderSummary>> GetOrdersAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<PagedResult<AdminInvoiceSummary>> GetInvoicesAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<PagedResult<AdminShipmentSummary>> GetShipmentsAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<PagedResult<AdminCampaignSummary>> GetCampaignsAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<PagedResult<AdminCouponSummary>> GetCouponsAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<PagedResult<AdminReturnSummary>> GetReturnsAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<PagedResult<AdminReviewSummary>> GetReviewsAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<PagedResult<AdminContactMessageSummary>> GetContactMessagesAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<InvoiceFile?> OpenInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken);
    Task<ServiceResult> ChangeOrderStatusAsync(Guid adminUserId, Guid orderId, OrderStatus status, string description, CancellationToken cancellationToken);
    Task<ServiceResult> CreateShipmentAsync(Guid adminUserId, AdminShipmentInput input, CancellationToken cancellationToken);
    Task<ServiceResult> TrackShipmentAsync(Guid adminUserId, Guid orderId, CancellationToken cancellationToken);
    Task<ServiceResult> CancelShipmentAsync(Guid adminUserId, Guid orderId, CancellationToken cancellationToken);
    Task<Guid> SaveCampaignAsync(Guid adminUserId, AdminCampaignInput input, CancellationToken cancellationToken);
    Task<Guid> SaveCouponAsync(Guid adminUserId, AdminCouponInput input, CancellationToken cancellationToken);
    Task<ServiceResult> ModerateReviewAsync(Guid adminUserId, Guid reviewId, ReviewStatus status, string? response, CancellationToken cancellationToken);
    Task<ServiceResult> UpdateContactStatusAsync(Guid adminUserId, Guid messageId, ContactMessageStatus status, CancellationToken cancellationToken);
}

public interface INotificationQueue
{
    Task EnqueueOrderAsync(Order order, string templateKey, CancellationToken cancellationToken);
}

public interface IPaymentGateway
{
    string ProviderName { get; }
    Task<PaymentInitializationResult> InitializeAsync(PaymentRequest request, CancellationToken cancellationToken);
    Task<PaymentVerificationResult> VerifyAsync(PaymentCallbackRequest request, CancellationToken cancellationToken);
    Task<RefundResult> RefundAsync(RefundRequest request, CancellationToken cancellationToken);
}

public interface IPaymentWebhookVerifier
{
    string ProviderName { get; }
    ValueTask<PaymentWebhookAuthenticationResult> AuthenticateAsync(PaymentWebhookEnvelope envelope, CancellationToken cancellationToken);
}

public interface IShippingProvider
{
    string ProviderName { get; }
    Task<ShipmentCreateResult> CreateShipmentAsync(ShipmentCreateRequest request, CancellationToken cancellationToken);
    Task<ShipmentTrackingResult> TrackAsync(string trackingNumber, CancellationToken cancellationToken);
    Task<ShipmentCancelResult> CancelAsync(string trackingNumber, CancellationToken cancellationToken);
}

public interface IInvoicePdfGenerator
{
    Task<byte[]> GenerateAsync(InvoiceDocument document, CancellationToken cancellationToken);
}

public interface IInvoiceStorage
{
    Task<string> SaveAsync(string invoiceNumber, ReadOnlyMemory<byte> content, CancellationToken cancellationToken);
    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken);
}

public interface IEmailSender
{
    Task<DeliveryResult> SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

public interface ISmsSender
{
    Task<DeliveryResult> SendAsync(SmsMessage message, CancellationToken cancellationToken);
}

public interface IFileStorageService
{
    Task<StoredFile> SaveAsync(Stream content, string originalFileName, string contentType, CancellationToken cancellationToken);
    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken);
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);
}
