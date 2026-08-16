using AETKAHVE.Application.Commerce;
using AETKAHVE.Domain.Common;
using AETKAHVE.Domain.Commerce;
using AETKAHVE.Infrastructure.Options;
using AETKAHVE.Infrastructure.Persistence;
using AETKAHVE.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AETKAHVE.Infrastructure.Commerce;

public sealed class AdminCommerceService(
    AppDbContext dbContext,
    IReportingService reportingService,
    IEnumerable<IShippingProvider> shippingProviders,
    IInvoiceStorage invoiceStorage,
    IOptions<ShippingOptions> shippingOptions,
    SecurityAuditWriter auditWriter,
    INotificationQueue notificationQueue,
    TimeProvider timeProvider) : IAdminCommerceService
{
    private readonly ShippingOptions _shippingOptions = shippingOptions.Value;

    public async Task<AdminDashboardSummary> GetDashboardAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var sales = await reportingService.GetSalesAsync(new ReportFilter(now.AddDays(-30), now), cancellationToken);
        var activeProductCount = await dbContext.Products.AsNoTracking()
            .CountAsync(product => product.IsActive, cancellationToken);
        var criticalStockCount = await dbContext.Products.AsNoTracking()
            .CountAsync(product => product.IsActive && product.StockQuantity <= product.CriticalStockLevel, cancellationToken);
        var ordersAwaitingActionCount = await dbContext.Orders.AsNoTracking()
            .CountAsync(order => order.Status == OrderStatus.PaymentReceived
                || order.Status == OrderStatus.Preparing
                || order.Status == OrderStatus.Packed, cancellationToken);
        var shipmentsInTransitCount = await dbContext.Shipments.AsNoTracking()
            .CountAsync(shipment => shipment.Status == ShipmentStatus.Created
                || shipment.Status == ShipmentStatus.Shipped
                || shipment.Status == ShipmentStatus.OutForDelivery, cancellationToken);
        var openReturnCount = await dbContext.ReturnRequests.AsNoTracking()
            .CountAsync(request => request.Status != ReturnStatus.Completed
                && request.Status != ReturnStatus.Rejected
                && request.Status != ReturnStatus.Cancelled, cancellationToken);
        var pendingReviewCount = await dbContext.Reviews.AsNoTracking()
            .CountAsync(review => review.Status == ReviewStatus.Pending, cancellationToken);
        var newMessageCount = await dbContext.ContactMessages.AsNoTracking()
            .CountAsync(message => message.Status == ContactMessageStatus.New, cancellationToken);
        var activeCampaignCount = IsSqlite
            ? (await dbContext.Campaigns.AsNoTracking()
                .Where(campaign => campaign.IsActive)
                .Select(campaign => new { campaign.StartDateUtc, campaign.EndDateUtc })
                .ToListAsync(cancellationToken))
                .Count(campaign => campaign.StartDateUtc <= now && campaign.EndDateUtc > now)
            : await dbContext.Campaigns.AsNoTracking()
                .CountAsync(campaign => campaign.IsActive
                    && campaign.StartDateUtc <= now
                    && campaign.EndDateUtc > now, cancellationToken);

        var orders = dbContext.Orders.AsNoTracking();
        var ordered = IsSqlite
            ? orders.OrderByDescending(order => order.Id)
            : orders.OrderByDescending(order => order.CreatedAtUtc);
        var recentOrders = await ordered.Take(5)
            .Select(order => new OrderSummary(
                order.Id,
                order.OrderNumber,
                order.Status,
                order.PaymentStatus,
                order.GrandTotal,
                order.Currency,
                order.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return new AdminDashboardSummary(
            sales,
            activeProductCount,
            criticalStockCount,
            ordersAwaitingActionCount,
            shipmentsInTransitCount,
            openReturnCount,
            pendingReviewCount,
            newMessageCount,
            activeCampaignCount,
            recentOrders,
            now);
    }

    public async Task<Guid> SaveCatalogLookupAsync(Guid adminUserId, AdminCatalogLookupInput input, CancellationToken cancellationToken)
    {
        var kind = Required(input.Kind, 30).ToLowerInvariant();
        var id = kind switch
        {
            "category" => await SaveLookupAsync(dbContext.Categories, input, cancellationToken),
            "brand" => await SaveLookupAsync(dbContext.Brands, input, cancellationToken),
            "coffeetype" => await SaveLookupAsync(dbContext.CoffeeTypes, input, cancellationToken),
            "beantype" => await SaveLookupAsync(dbContext.BeanTypes, input, cancellationToken),
            "roastlevel" => await SaveLookupAsync(dbContext.RoastLevels, input, cancellationToken),
            "origin" => await SaveOriginAsync(input, cancellationToken),
            _ => throw new CommerceRuleException("Catalog lookup kind is invalid."),
        };
        await dbContext.SaveChangesAsync(cancellationToken);
        await AuditAsync(adminUserId, "CatalogLookupSaved", id, cancellationToken);
        return id;
    }

    public async Task<Guid> SaveProductAsync(Guid adminUserId, AdminProductInput input, CancellationToken cancellationToken)
    {
        var product = input.Id is null
            ? new Product { CreatedAtUtc = timeProvider.GetUtcNow() }
            : await dbContext.Products.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == input.Id, cancellationToken)
                ?? throw new CommerceRuleException("Product was not found.");
        if (input.Id is null) dbContext.Products.Add(product);
        product.Name = Required(input.Name, 200); product.Slug = Required(input.Slug, 220).ToLowerInvariant(); product.Sku = Required(input.Sku, 80).ToUpperInvariant();
        product.ShortDescription = Required(input.ShortDescription, 500); product.Description = Required(input.Description, 8000);
        product.BasePrice = input.BasePrice; product.DiscountedPrice = input.DiscountedPrice; product.TaxRate = input.TaxRate;
        product.StockQuantity = input.StockQuantity; product.CriticalStockLevel = input.CriticalStockLevel; product.CategoryId = input.CategoryId;
        product.BrandId = input.BrandId; product.CoffeeTypeId = input.CoffeeTypeId; product.BeanTypeId = input.BeanTypeId;
        product.RoastLevelId = input.RoastLevelId; product.OriginId = input.OriginId; product.IsActive = input.IsActive; product.IsFeatured = input.IsFeatured;
        product.UpdatedAtUtc = timeProvider.GetUtcNow(); product.ConcurrencyToken = Guid.NewGuid(); product.Validate();
        if (!await dbContext.Categories.AnyAsync(x => x.Id == input.CategoryId && x.IsActive, cancellationToken)) throw new CommerceRuleException("Category is invalid.");
        await dbContext.SaveChangesAsync(cancellationToken);
        await AuditAsync(adminUserId, input.Id is null ? "ProductCreated" : "ProductUpdated", product.Id, cancellationToken);
        return product.Id;
    }

    public async Task<ServiceResult> AdjustStockAsync(Guid adminUserId, Guid productId, Guid? variantId, int delta, CancellationToken cancellationToken)
    {
        if (delta == 0) return ServiceResult.Failure("Stock delta cannot be zero.");
        var product = await dbContext.Products.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == productId, cancellationToken);
        if (product is null) return ServiceResult.Failure("Product was not found.");
        int previous;
        int next;
        if (variantId is not null)
        {
            var variant = await dbContext.ProductVariants.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == variantId && x.ProductId == productId, cancellationToken);
            if (variant is null) return ServiceResult.Failure("Variant was not found.");
            previous = variant.StockQuantity; variant.AdjustStock(delta); next = variant.StockQuantity;
        }
        else { previous = product.StockQuantity; product.AdjustStock(delta); next = product.StockQuantity; }
        var now = timeProvider.GetUtcNow();
        dbContext.StockMovements.Add(new StockMovement
        {
            ProductId = productId,
            ProductVariantId = variantId,
            MovementType = delta > 0 ? StockMovementType.ManualIncrease : StockMovementType.ManualDecrease,
            Quantity = delta,
            PreviousStock = previous,
            NewStock = next,
            ReferenceType = "AdminAdjustment",
            ReferenceId = Guid.NewGuid(),
            Description = "Manual stock adjustment.",
            CreatedByUserId = adminUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await AuditAsync(adminUserId, "StockAdjusted", productId, cancellationToken);
        return ServiceResult.Success("Stock was updated.");
    }

    public async Task<PagedResult<OrderSummary>> GetOrdersAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var source = dbContext.Orders.AsNoTracking();
        var query = dbContext.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true
            ? source.OrderByDescending(x => x.Id)
            : source.OrderByDescending(x => x.CreatedAtUtc);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new OrderSummary(x.Id, x.OrderNumber, x.Status, x.PaymentStatus, x.GrandTotal, x.Currency, x.CreatedAtUtc)).ToListAsync(cancellationToken);
        return new PagedResult<OrderSummary>(items, page, pageSize, total);
    }

    public async Task<PagedResult<AdminInvoiceSummary>> GetInvoicesAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var source = dbContext.Invoices.AsNoTracking();
        var query = dbContext.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true
            ? source.OrderByDescending(x => x.Id)
            : source.OrderByDescending(x => x.InvoiceDateUtc);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new AdminInvoiceSummary(x.Id, x.InvoiceNumber, x.Order.OrderNumber, x.GrandTotal, x.Currency, x.InvoiceDateUtc))
            .ToListAsync(cancellationToken);
        return new PagedResult<AdminInvoiceSummary>(items, page, pageSize, total);
    }

    public async Task<InvoiceFile?> OpenInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken)
    {
        var invoice = await dbContext.Invoices.AsNoTracking().Where(x => x.Id == invoiceId)
            .Select(x => new { x.StorageKey, x.InvoiceNumber }).SingleOrDefaultAsync(cancellationToken);
        if (invoice is null) return null;
        var stream = await invoiceStorage.OpenReadAsync(invoice.StorageKey, cancellationToken);
        return stream is null ? null : new InvoiceFile(stream, $"{invoice.InvoiceNumber}.pdf");
    }

    public async Task<PagedResult<AdminShipmentSummary>> GetShipmentsAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var source = dbContext.Shipments.AsNoTracking();
        var query = IsSqlite ? source.OrderByDescending(x => x.Id) : source.OrderByDescending(x => x.UpdatedAtUtc);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new AdminShipmentSummary(x.Id, x.OrderId, x.Order.OrderNumber, x.Status, x.TrackingNumber, x.UpdatedAtUtc)).ToListAsync(cancellationToken);
        return new PagedResult<AdminShipmentSummary>(items, page, pageSize, total);
    }

    public async Task<PagedResult<AdminCampaignSummary>> GetCampaignsAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var source = dbContext.Campaigns.AsNoTracking();
        var query = IsSqlite ? source.OrderByDescending(x => x.Id) : source.OrderByDescending(x => x.StartDateUtc);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new AdminCampaignSummary(x.Id, x.Name, x.Slug, x.DiscountType, x.DiscountValue, x.IsActive, x.StartDateUtc, x.EndDateUtc)).ToListAsync(cancellationToken);
        return new PagedResult<AdminCampaignSummary>(items, page, pageSize, total);
    }

    public async Task<PagedResult<AdminCouponSummary>> GetCouponsAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var source = dbContext.Coupons.AsNoTracking().OrderBy(x => x.Code);
        var total = await source.CountAsync(cancellationToken);
        var items = await source.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new AdminCouponSummary(x.Id, x.Name, x.Code, x.DiscountType, x.DiscountValue, x.IsActive,
                x.Usages.Count(y => y.Status == CouponUsageStatus.Consumed), x.TotalUsageLimit)).ToListAsync(cancellationToken);
        return new PagedResult<AdminCouponSummary>(items, page, pageSize, total);
    }

    public async Task<PagedResult<AdminReturnSummary>> GetReturnsAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var source = dbContext.ReturnRequests.AsNoTracking();
        var query = IsSqlite ? source.OrderByDescending(x => x.Id) : source.OrderByDescending(x => x.RequestedAtUtc);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new AdminReturnSummary(x.Id, x.Order.OrderNumber, x.UserId, x.Status, x.RefundAmount, x.RequestedAtUtc)).ToListAsync(cancellationToken);
        return new PagedResult<AdminReturnSummary>(items, page, pageSize, total);
    }

    public async Task<PagedResult<AdminReviewSummary>> GetReviewsAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var source = dbContext.Reviews.AsNoTracking();
        var query = IsSqlite ? source.OrderByDescending(x => x.Id) : source.OrderByDescending(x => x.CreatedAtUtc);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new AdminReviewSummary(x.Id, x.Product.Name, x.UserId, x.Rating, x.Status, x.CreatedAtUtc)).ToListAsync(cancellationToken);
        return new PagedResult<AdminReviewSummary>(items, page, pageSize, total);
    }

    public async Task<PagedResult<AdminContactMessageSummary>> GetContactMessagesAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var source = dbContext.ContactMessages.AsNoTracking();
        var query = IsSqlite ? source.OrderByDescending(x => x.Id) : source.OrderByDescending(x => x.CreatedAtUtc);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new AdminContactMessageSummary(x.Id, x.FullName, x.Email, x.Subject, x.Status, x.CreatedAtUtc)).ToListAsync(cancellationToken);
        return new PagedResult<AdminContactMessageSummary>(items, page, pageSize, total);
    }

    public async Task<ServiceResult> ChangeOrderStatusAsync(Guid adminUserId, Guid orderId, OrderStatus status, string description, CancellationToken cancellationToken)
    {
        var order = await dbContext.Orders.Include(x => x.StatusHistory).Include(x => x.Shipment).SingleOrDefaultAsync(x => x.Id == orderId, cancellationToken);
        if (order is null) return ServiceResult.Failure("Order was not found.");
        if ((status is OrderStatus.Shipped or OrderStatus.OutForDelivery or OrderStatus.Delivered) && order.Shipment is null)
            return ServiceResult.Failure("Shipment must be created before applying this order status.");
        if (status == OrderStatus.Cancelled && order.PaymentStatus == PaymentStatus.Succeeded)
            return ServiceResult.Failure("Paid orders must use the refund-aware cancellation workflow.");
        try { dbContext.OrderStatusHistory.Add(order.TransitionTo(status, adminUserId, timeProvider.GetUtcNow(), Required(description, 500))); }
        catch (CommerceRuleException exception) { return ServiceResult.Failure(exception.Message); }
        var shipmentStatus = status switch
        {
            OrderStatus.Shipped => ShipmentStatus.Shipped,
            OrderStatus.OutForDelivery => ShipmentStatus.OutForDelivery,
            OrderStatus.Delivered => ShipmentStatus.Delivered,
            OrderStatus.Cancelled => ShipmentStatus.Cancelled,
            _ => (ShipmentStatus?)null,
        };
        if (shipmentStatus is not null)
        {
            var now = timeProvider.GetUtcNow();
            if (order.Shipment is not null && order.Shipment.Status != shipmentStatus)
            {
                dbContext.ShipmentStatusHistory.Add(new ShipmentStatusHistory
                {
                    ShipmentId = order.Shipment.Id,
                    Shipment = order.Shipment,
                    PreviousStatus = order.Shipment.Status,
                    NewStatus = shipmentStatus.Value,
                    Description = description,
                    ChangedByUserId = adminUserId,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now,
                });
                order.Shipment.Status = shipmentStatus.Value;
                order.Shipment.UpdatedAtUtc = now;
            }
            order.ShippingStatus = shipmentStatus.Value;
            if (status == OrderStatus.Shipped) { order.ShippedAtUtc = now; if (order.Shipment is not null) order.Shipment.ShippedAtUtc = now; }
            if (status == OrderStatus.Delivered) { order.DeliveredAtUtc = now; if (order.Shipment is not null) order.Shipment.DeliveredAtUtc = now; }
        }
        await notificationQueue.EnqueueOrderAsync(order, $"Order{status}", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await AuditAsync(adminUserId, "OrderStatusChanged", orderId, cancellationToken);
        return ServiceResult.Success("Order status was updated.");
    }

    public async Task<ServiceResult> CreateShipmentAsync(Guid adminUserId, AdminShipmentInput input, CancellationToken cancellationToken)
    {
        var order = await dbContext.Orders.Include(x => x.Shipment).SingleOrDefaultAsync(x => x.Id == input.OrderId, cancellationToken);
        if (order is null) return ServiceResult.Failure("Order was not found.");
        if (order.PaymentStatus != PaymentStatus.Succeeded || order.Status is OrderStatus.PendingPayment or OrderStatus.Cancelled) return ServiceResult.Failure("Order is not ready for shipment.");
        var provider = shippingProviders.SingleOrDefault(x => x.ProviderName.Equals(_shippingOptions.Provider, StringComparison.OrdinalIgnoreCase));
        if (provider is null) return ServiceResult.Failure("Shipping provider is not configured.");
        var result = await provider.CreateShipmentAsync(new ShipmentCreateRequest(order.Id, order.OrderNumber, order.ShippingAddressSnapshot), cancellationToken);
        if (!result.Succeeded) return ServiceResult.Failure(result.FailureReason ?? "Shipment could not be created.");
        var now = timeProvider.GetUtcNow();
        var shipment = order.Shipment ?? new Shipment { Order = order, CreatedAtUtc = now };
        if (order.Shipment is null) dbContext.Shipments.Add(shipment);
        var previousStatus = shipment.Status;
        shipment.ShippingCompany = _shippingOptions.CompanyName; shipment.TrackingNumber = result.TrackingNumber; shipment.TrackingUrl = result.TrackingUrl;
        shipment.Status = ShipmentStatus.Created; shipment.EstimatedDeliveryDateUtc = input.EstimatedDeliveryDateUtc?.ToUniversalTime(); shipment.ShippingNote = Truncate(input.Note, 1000); shipment.UpdatedAtUtc = now;
        dbContext.ShipmentStatusHistory.Add(new ShipmentStatusHistory
        {
            Shipment = shipment,
            PreviousStatus = previousStatus,
            NewStatus = ShipmentStatus.Created,
            Description = "Shipment created.",
            ChangedByUserId = adminUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        });
        order.ShippingStatus = ShipmentStatus.Created;
        await dbContext.SaveChangesAsync(cancellationToken);
        await AuditAsync(adminUserId, "ShipmentCreated", order.Id, cancellationToken);
        return ServiceResult.Success("Shipment was created.");
    }

    public async Task<ServiceResult> TrackShipmentAsync(Guid adminUserId, Guid orderId, CancellationToken cancellationToken)
    {
        var shipment = await dbContext.Shipments.Include(x => x.Order).SingleOrDefaultAsync(x => x.OrderId == orderId, cancellationToken);
        if (shipment?.TrackingNumber is null) return ServiceResult.Failure("Shipment was not found.");
        var provider = ResolveShippingProvider();
        var result = await provider.TrackAsync(shipment.TrackingNumber, cancellationToken);
        if (!result.Succeeded) return ServiceResult.Failure(result.Description ?? "Shipment could not be tracked.");
        var previous = shipment.Status;
        var now = timeProvider.GetUtcNow();
        shipment.Status = result.Status; shipment.UpdatedAtUtc = now; shipment.Order.ShippingStatus = result.Status;
        if (result.Status == ShipmentStatus.Shipped) { shipment.ShippedAtUtc ??= now; shipment.Order.ShippedAtUtc ??= now; }
        if (result.Status == ShipmentStatus.Delivered) { shipment.DeliveredAtUtc ??= now; shipment.Order.DeliveredAtUtc ??= now; }
        if (previous != result.Status)
        {
            dbContext.ShipmentStatusHistory.Add(new ShipmentStatusHistory
            {
                ShipmentId = shipment.Id,
                Shipment = shipment,
                PreviousStatus = previous,
                NewStatus = result.Status,
                Description = Truncate(result.Description, 500),
                ChangedByUserId = adminUserId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            });
            if (OrderStatusRules.CanTransition(shipment.Order.Status, ToOrderStatus(result.Status)))
                dbContext.OrderStatusHistory.Add(shipment.Order.TransitionTo(ToOrderStatus(result.Status), adminUserId, now, result.Description ?? "Shipment status updated."));
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        await AuditAsync(adminUserId, "ShipmentTracked", orderId, cancellationToken);
        return ServiceResult.Success(result.Description ?? "Shipment status was updated.");
    }

    public async Task<ServiceResult> CancelShipmentAsync(Guid adminUserId, Guid orderId, CancellationToken cancellationToken)
    {
        var shipment = await dbContext.Shipments.Include(x => x.Order).SingleOrDefaultAsync(x => x.OrderId == orderId, cancellationToken);
        if (shipment?.TrackingNumber is null) return ServiceResult.Failure("Shipment was not found.");
        if (shipment.Status is ShipmentStatus.Shipped or ShipmentStatus.OutForDelivery or ShipmentStatus.Delivered or ShipmentStatus.Cancelled)
            return ServiceResult.Failure("Shipment can no longer be cancelled.");
        var result = await ResolveShippingProvider().CancelAsync(shipment.TrackingNumber, cancellationToken);
        if (!result.Succeeded) return ServiceResult.Failure(result.FailureReason ?? "Shipment could not be cancelled.");
        var now = timeProvider.GetUtcNow();
        dbContext.ShipmentStatusHistory.Add(new ShipmentStatusHistory
        {
            ShipmentId = shipment.Id,
            Shipment = shipment,
            PreviousStatus = shipment.Status,
            NewStatus = ShipmentStatus.Cancelled,
            Description = "Shipment cancelled.",
            ChangedByUserId = adminUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        });
        shipment.Status = ShipmentStatus.Cancelled; shipment.UpdatedAtUtc = now; shipment.Order.ShippingStatus = ShipmentStatus.Cancelled;
        await dbContext.SaveChangesAsync(cancellationToken);
        await AuditAsync(adminUserId, "ShipmentCancelled", orderId, cancellationToken);
        return ServiceResult.Success("Shipment was cancelled.");
    }

    public async Task<Guid> SaveCampaignAsync(Guid adminUserId, AdminCampaignInput input, CancellationToken cancellationToken)
    {
        ValidatePromotion(input.DiscountType, input.DiscountValue, input.StartDateUtc, input.EndDateUtc);
        if (input.MinimumCartAmount < 0 || input.MaximumDiscountAmount < 0)
            throw new CommerceRuleException("Kampanya tutar sınırları negatif olamaz.");
        var name = Required(input.Name, 180);
        var slug = Slug(input.Slug, 200);
        if (await dbContext.Campaigns.AnyAsync(
                x => x.Slug == slug && (input.Id == null || x.Id != input.Id.Value),
                cancellationToken))
            throw new CommerceRuleException("Bu kampanya adıyla oluşturulan bağlantı zaten kullanılıyor. Kampanya adını değiştirin.");
        var entity = input.Id is null ? new Campaign { CreatedAtUtc = timeProvider.GetUtcNow() } : await dbContext.Campaigns
            .Include(x => x.Products).Include(x => x.Categories).SingleAsync(x => x.Id == input.Id, cancellationToken);
        if (input.Id is null) dbContext.Campaigns.Add(entity);
        entity.Name = name; entity.Slug = slug; entity.DiscountType = input.DiscountType;
        entity.DiscountValue = input.DiscountValue; entity.MinimumCartAmount = input.MinimumCartAmount; entity.MaximumDiscountAmount = input.MaximumDiscountAmount;
        entity.StartDateUtc = input.StartDateUtc.ToUniversalTime(); entity.EndDateUtc = input.EndDateUtc.ToUniversalTime(); entity.IsActive = input.IsActive; entity.CanCombineWithOtherDiscounts = input.CanCombineWithOtherDiscounts; entity.UpdatedAtUtc = timeProvider.GetUtcNow();
        await SynchronizeCampaignTargetsAsync(entity, input.ProductIds ?? [], input.CategoryIds ?? [], cancellationToken);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception, "IX_Campaigns_Slug", "Campaigns.Slug"))
        {
            throw new CommerceRuleException("Bu kampanya adıyla oluşturulan bağlantı zaten kullanılıyor. Kampanya adını değiştirin.");
        }
        await AuditAsync(adminUserId, "CampaignSaved", entity.Id, cancellationToken); return entity.Id;
    }

    public async Task<Guid> SaveCouponAsync(Guid adminUserId, AdminCouponInput input, CancellationToken cancellationToken)
    {
        ValidatePromotion(input.DiscountType, input.DiscountValue, input.StartDateUtc, input.EndDateUtc);
        if (input.MinimumCartAmount < 0 || input.MaximumDiscountAmount < 0 || input.TotalUsageLimit <= 0 || input.PerUserUsageLimit <= 0)
            throw new CommerceRuleException("Kupon tutar sınırları negatif, kullanım limitleri ise sıfır veya negatif olamaz.");
        var name = Required(input.Name, 180);
        var code = Code(input.Code, 80);
        if (await dbContext.Coupons.AnyAsync(
                x => x.Code == code && (input.Id == null || x.Id != input.Id.Value),
                cancellationToken))
            throw new CommerceRuleException("Bu kupon kodu zaten kullanılıyor. Farklı bir kod girin.");
        var entity = input.Id is null ? new Coupon { CreatedAtUtc = timeProvider.GetUtcNow() } : await dbContext.Coupons.SingleAsync(x => x.Id == input.Id, cancellationToken);
        if (input.Id is null) dbContext.Coupons.Add(entity);
        entity.Name = name; entity.Code = code; entity.DiscountType = input.DiscountType;
        entity.DiscountValue = input.DiscountValue; entity.MinimumCartAmount = input.MinimumCartAmount; entity.MaximumDiscountAmount = input.MaximumDiscountAmount;
        entity.StartDateUtc = input.StartDateUtc.ToUniversalTime(); entity.EndDateUtc = input.EndDateUtc.ToUniversalTime(); entity.TotalUsageLimit = input.TotalUsageLimit; entity.PerUserUsageLimit = input.PerUserUsageLimit;
        entity.IsFirstOrderOnly = input.IsFirstOrderOnly; entity.IsActive = input.IsActive; entity.CanCombineWithOtherDiscounts = input.CanCombineWithOtherDiscounts; entity.UpdatedAtUtc = timeProvider.GetUtcNow();
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception, "IX_Coupons_Code", "Coupons.Code"))
        {
            throw new CommerceRuleException("Bu kupon kodu zaten kullanılıyor. Farklı bir kod girin.");
        }
        await AuditAsync(adminUserId, "CouponSaved", entity.Id, cancellationToken); return entity.Id;
    }

    public async Task<ServiceResult> ModerateReviewAsync(Guid adminUserId, Guid reviewId, ReviewStatus status, string? response, CancellationToken cancellationToken)
    {
        if (status == ReviewStatus.Pending) return ServiceResult.Failure("Review must be approved or rejected.");
        var review = await dbContext.Reviews.SingleOrDefaultAsync(x => x.Id == reviewId, cancellationToken);
        if (review is null) return ServiceResult.Failure("Review was not found.");
        review.Status = status; review.AdminResponse = Truncate(response, 1000); review.UpdatedAtUtc = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken); await AuditAsync(adminUserId, "ReviewModerated", reviewId, cancellationToken);
        return ServiceResult.Success("Review was moderated.");
    }

    public async Task<ServiceResult> UpdateContactStatusAsync(Guid adminUserId, Guid messageId, ContactMessageStatus status, CancellationToken cancellationToken)
    {
        var message = await dbContext.ContactMessages.SingleOrDefaultAsync(x => x.Id == messageId, cancellationToken);
        if (message is null) return ServiceResult.Failure("Contact message was not found.");
        message.Status = status; message.AnsweredByUserId = status == ContactMessageStatus.Answered ? adminUserId : null;
        message.AnsweredAtUtc = status == ContactMessageStatus.Answered ? timeProvider.GetUtcNow() : null; message.UpdatedAtUtc = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken); await AuditAsync(adminUserId, "ContactStatusChanged", messageId, cancellationToken);
        return ServiceResult.Success("Contact message was updated.");
    }

    private Task AuditAsync(Guid actor, string action, Guid entityId, CancellationToken cancellationToken) =>
        auditWriter.WriteAsync(action, $"{action} for commerce entity {entityId}.", actor, null, null, null, Guid.NewGuid().ToString("N"), cancellationToken);
    private static void ValidatePromotion(DiscountType type, decimal value, DateTimeOffset start, DateTimeOffset end)
    {
        if (!Enum.IsDefined(type))
            throw new CommerceRuleException("Geçerli bir indirim türü seçin.");
        if (end <= start)
            throw new CommerceRuleException("Bitiş tarihi başlangıç tarihinden sonra olmalıdır.");
        if (type == DiscountType.FreeShipping && value != 0)
            throw new CommerceRuleException("Ücretsiz kargo indiriminin değeri 0 olmalıdır.");
        if (type != DiscountType.FreeShipping && value <= 0)
            throw new CommerceRuleException("İndirim değeri 0'dan büyük olmalıdır.");
        if (type == DiscountType.Percentage && value > 100)
            throw new CommerceRuleException("Yüzde indirimi 100'den büyük olamaz.");
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception, params string[] markers)
    {
        var messages = new List<string>();
        for (Exception? current = exception; current is not null; current = current.InnerException)
            messages.Add(current.Message);
        var combined = string.Join(' ', messages);
        return (combined.Contains("unique", StringComparison.OrdinalIgnoreCase)
                || combined.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
            && markers.Any(marker => combined.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private async Task SynchronizeCampaignTargetsAsync(Campaign campaign, IReadOnlyCollection<Guid> productIds, IReadOnlyCollection<Guid> categoryIds, CancellationToken cancellationToken)
    {
        var products = productIds.Distinct().ToHashSet();
        var categories = categoryIds.Distinct().ToHashSet();
        if (products.Count != await dbContext.Products.CountAsync(x => products.Contains(x.Id) && x.IsActive, cancellationToken))
            throw new CommerceRuleException("Kampanya hedeflerindeki ürün ID'lerinden biri geçersiz veya pasif.");
        if (categories.Count != await dbContext.Categories.CountAsync(x => categories.Contains(x.Id) && x.IsActive, cancellationToken))
            throw new CommerceRuleException("Kampanya hedeflerindeki kategori ID'lerinden biri geçersiz veya pasif.");

        dbContext.CampaignProducts.RemoveRange(campaign.Products.Where(x => !products.Contains(x.ProductId)));
        dbContext.CampaignCategories.RemoveRange(campaign.Categories.Where(x => !categories.Contains(x.CategoryId)));
        foreach (var productId in products.Where(x => campaign.Products.All(y => y.ProductId != x)))
            dbContext.CampaignProducts.Add(new CampaignProduct { CampaignId = campaign.Id, Campaign = campaign, ProductId = productId });
        foreach (var categoryId in categories.Where(x => campaign.Categories.All(y => y.CategoryId != x)))
            dbContext.CampaignCategories.Add(new CampaignCategory { CampaignId = campaign.Id, Campaign = campaign, CategoryId = categoryId });
    }
    private async Task<Guid> SaveLookupAsync<T>(DbSet<T> set, AdminCatalogLookupInput input, CancellationToken cancellationToken) where T : CatalogLookup, new()
    {
        var entity = input.Id is null ? new T { CreatedAtUtc = timeProvider.GetUtcNow() } : await set.IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.Id == input.Id.Value, cancellationToken) ?? throw new CommerceRuleException("Catalog lookup was not found.");
        if (input.Id is null) set.Add(entity);
        entity.Name = Required(input.Name, 150); entity.Slug = Slug(input.Slug, 160); entity.IsActive = input.IsActive;
        entity.DeletedAtUtc = null; entity.UpdatedAtUtc = timeProvider.GetUtcNow();
        return entity.Id;
    }
    private async Task<Guid> SaveOriginAsync(AdminCatalogLookupInput input, CancellationToken cancellationToken)
    {
        var origin = input.Id is null ? new Origin { CreatedAtUtc = timeProvider.GetUtcNow() } : await dbContext.Origins.IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.Id == input.Id.Value, cancellationToken) ?? throw new CommerceRuleException("Catalog lookup was not found.");
        if (input.Id is null) dbContext.Origins.Add(origin);
        origin.Name = Required(input.Name, 150); origin.Slug = Slug(input.Slug, 160); origin.IsActive = input.IsActive;
        origin.DeletedAtUtc = null; origin.UpdatedAtUtc = timeProvider.GetUtcNow();
        origin.CountryCode = string.IsNullOrWhiteSpace(input.CountryCode) ? null : Required(input.CountryCode, 2).ToUpperInvariant();
        return origin.Id;
    }
    private static string Required(string value, int max) => string.IsNullOrWhiteSpace(value) ? throw new CommerceRuleException("Required value is missing.") : value.Trim()[..Math.Min(max, value.Trim().Length)];
    private static string? Truncate(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(max, value.Trim().Length)];
    private static string Slug(string value, int max)
    {
        var slug = Required(value, max).ToLowerInvariant();
        return System.Text.RegularExpressions.Regex.IsMatch(slug, "^[a-z0-9]+(?:-[a-z0-9]+)*$", System.Text.RegularExpressions.RegexOptions.CultureInvariant)
            ? slug
            : throw new CommerceRuleException("Slug must contain lowercase ASCII letters, numbers and single hyphens.");
    }
    private static string Code(string value, int max)
    {
        var code = Required(value, max).ToUpperInvariant();
        return System.Text.RegularExpressions.Regex.IsMatch(code, "^[A-Z0-9]+(?:-[A-Z0-9]+)*$", System.Text.RegularExpressions.RegexOptions.CultureInvariant)
            ? code
            : throw new CommerceRuleException("Code must contain uppercase ASCII letters, numbers and single hyphens.");
    }
    private IShippingProvider ResolveShippingProvider() => shippingProviders.SingleOrDefault(x => x.ProviderName.Equals(_shippingOptions.Provider, StringComparison.OrdinalIgnoreCase))
        ?? throw new CommerceRuleException("Shipping provider is not configured.");
    private static OrderStatus ToOrderStatus(ShipmentStatus status) => status switch
    {
        ShipmentStatus.Shipped => OrderStatus.Shipped,
        ShipmentStatus.OutForDelivery => OrderStatus.OutForDelivery,
        ShipmentStatus.Delivered => OrderStatus.Delivered,
        _ => OrderStatus.PendingPayment,
    };
    private bool IsSqlite => dbContext.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;
}

public sealed class NotificationService(AppDbContext dbContext, TimeProvider timeProvider) : INotificationService
{
    public async Task<IReadOnlyList<NotificationItem>> GetAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.Notifications.AsNoTracking().Where(x => x.UserId == userId).OrderByDescending(x => x.Id).Take(100)
            .Select(x => new NotificationItem(x.Id, x.Title, x.Message, x.Type, x.RelatedEntityType, x.RelatedEntityId, x.IsRead, x.CreatedAtUtc)).ToListAsync(cancellationToken);

    public async Task<ServiceResult> MarkReadAsync(Guid userId, Guid? notificationId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var count = await dbContext.Notifications.Where(x => x.UserId == userId && !x.IsRead && (notificationId == null || x.Id == notificationId))
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsRead, true).SetProperty(y => y.ReadAtUtc, now).SetProperty(y => y.UpdatedAtUtc, now), cancellationToken);
        return count == 0 && notificationId is not null ? ServiceResult.Failure("Notification was not found.") : ServiceResult.Success("Notifications were marked as read.");
    }
}
