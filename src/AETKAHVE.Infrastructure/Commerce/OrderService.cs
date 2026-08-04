using AETKAHVE.Application.Commerce;
using AETKAHVE.Domain.Common;
using AETKAHVE.Domain.Commerce;
using AETKAHVE.Infrastructure.Persistence;
using AETKAHVE.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AETKAHVE.Infrastructure.Commerce;

public sealed class OrderService(
    AppDbContext dbContext,
    IInventoryService inventoryService,
    IEnumerable<IPaymentGateway> paymentGateways,
    IEnumerable<IShippingProvider> shippingProviders,
    IInvoiceStorage invoiceStorage,
    INotificationQueue notificationQueue,
    IOptions<ShippingOptions> shippingOptions,
    TimeProvider timeProvider) : IOrderService
{
    public async Task<PagedResult<OrderSummary>> GetForUserAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var source = dbContext.Orders.AsNoTracking().Where(x => x.UserId == userId);
        var query = dbContext.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true
            ? source.OrderByDescending(x => x.Id)
            : source.OrderByDescending(x => x.CreatedAtUtc);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new OrderSummary(x.Id, x.OrderNumber, x.Status, x.PaymentStatus, x.GrandTotal, x.Currency, x.CreatedAtUtc)).ToListAsync(cancellationToken);
        return new PagedResult<OrderSummary>(items, page, pageSize, total);
    }

    public async Task<OrderDetails?> GetForUserAsync(Guid userId, Guid orderId, CancellationToken cancellationToken) =>
        await dbContext.Orders.AsNoTracking().Where(x => x.Id == orderId && x.UserId == userId)
            .Select(x => new OrderDetails(
                new OrderSummary(x.Id, x.OrderNumber, x.Status, x.PaymentStatus, x.GrandTotal, x.Currency, x.CreatedAtUtc),
                x.Items.Select(i => new InvoiceLine(i.ProductName, i.Sku, i.Quantity, i.UnitPrice, i.DiscountAmount, i.TaxAmount, i.LineTotal)).ToList(),
                x.ShippingStatus, x.ShippingAddressSnapshot, x.BillingAddressSnapshot)).SingleOrDefaultAsync(cancellationToken);

    public async Task<PagedResult<InvoiceSummary>> GetInvoicesForUserAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var source = dbContext.Invoices.AsNoTracking().Where(x => x.Order.UserId == userId);
        var query = dbContext.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true
            ? source.OrderByDescending(x => x.Id)
            : source.OrderByDescending(x => x.InvoiceDateUtc);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new InvoiceSummary(x.Id, x.InvoiceNumber, x.Order.OrderNumber, x.GrandTotal, x.Currency, x.InvoiceDateUtc))
            .ToListAsync(cancellationToken);
        return new PagedResult<InvoiceSummary>(items, page, pageSize, total);
    }

    public async Task<ServiceResult> CancelAsync(Guid userId, Guid orderId, CancellationToken cancellationToken)
    {
        var order = await dbContext.Orders.Include(x => x.Items).Include(x => x.Payments).Include(x => x.Shipment)
            .SingleOrDefaultAsync(x => x.Id == orderId && x.UserId == userId, cancellationToken);
        if (order is null) return ServiceResult.Failure("Order was not found.");
        if (order.Status is not (OrderStatus.PendingPayment or OrderStatus.PaymentReceived or OrderStatus.Preparing or OrderStatus.Packed)) return ServiceResult.Failure("Order can no longer be cancelled.");
        if (order.Shipment?.Status is ShipmentStatus.Shipped or ShipmentStatus.OutForDelivery or ShipmentStatus.Delivered) return ServiceResult.Failure("Shipped order cannot be cancelled.");

        if (!string.IsNullOrWhiteSpace(order.Shipment?.TrackingNumber))
        {
            var shippingProvider = shippingProviders.SingleOrDefault(x => x.ProviderName.Equals(shippingOptions.Value.Provider, StringComparison.OrdinalIgnoreCase));
            if (shippingProvider is null) return ServiceResult.Failure("Shipping provider is not configured.");
            var shipmentCancellation = await shippingProvider.CancelAsync(order.Shipment.TrackingNumber, cancellationToken);
            if (!shipmentCancellation.Succeeded) return ServiceResult.Failure(shipmentCancellation.FailureReason ?? "Shipment could not be cancelled.");
        }

        var payment = order.Payments.OrderByDescending(x => x.CreatedAtUtc).FirstOrDefault();
        RefundResult? refundResult = null;
        if (payment?.Status == PaymentStatus.Succeeded)
        {
            var gateway = paymentGateways.Single(x => x.ProviderName.Equals(payment.Provider, StringComparison.OrdinalIgnoreCase));
            refundResult = await gateway.RefundAsync(new RefundRequest(payment.Id, payment.TransactionId ?? string.Empty, payment.Amount, payment.Currency, "cancel"), cancellationToken);
            if (!refundResult.Succeeded) return ServiceResult.Failure("Refund could not be completed; cancellation was not applied.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (payment?.Status == PaymentStatus.Succeeded)
        {
            await inventoryService.RestoreForOrderAsync(order, StockMovementType.Cancellation, userId, cancellationToken);
            payment.Status = PaymentStatus.Refunded;
            order.PaymentStatus = PaymentStatus.Refunded;
            var now = timeProvider.GetUtcNow();
            dbContext.Refunds.Add(new Refund { PaymentId = payment.Id, Amount = payment.Amount, Status = RefundStatus.Succeeded, ProviderReference = refundResult!.ProviderReference, CompletedAtUtc = now, CreatedAtUtc = now, UpdatedAtUtc = now });
        }
        if (order.Shipment is not null && order.Shipment.Status != ShipmentStatus.Cancelled)
        {
            var now = timeProvider.GetUtcNow();
            dbContext.ShipmentStatusHistory.Add(new ShipmentStatusHistory
            {
                ShipmentId = order.Shipment.Id, Shipment = order.Shipment, PreviousStatus = order.Shipment.Status,
                NewStatus = ShipmentStatus.Cancelled, Description = "Order cancelled by customer.", ChangedByUserId = userId,
                CreatedAtUtc = now, UpdatedAtUtc = now,
            });
            order.Shipment.Status = ShipmentStatus.Cancelled;
            order.Shipment.UpdatedAtUtc = now;
            order.ShippingStatus = ShipmentStatus.Cancelled;
        }
        dbContext.OrderStatusHistory.Add(order.TransitionTo(OrderStatus.Cancelled, userId, timeProvider.GetUtcNow(), "Cancelled by customer."));
        order.CancelledAtUtc = timeProvider.GetUtcNow();
        await dbContext.CouponUsages.Where(x => x.OrderId == order.Id).ExecuteUpdateAsync(x => x.SetProperty(y => y.Status, CouponUsageStatus.Released), cancellationToken);
        await notificationQueue.EnqueueOrderAsync(order, "OrderCancelled", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ServiceResult.Success("Order was cancelled.");
    }

    public async Task<InvoiceFile?> OpenInvoiceAsync(Guid userId, Guid invoiceId, CancellationToken cancellationToken)
    {
        var invoice = await dbContext.Invoices.AsNoTracking().Where(x => x.Id == invoiceId && x.Order.UserId == userId)
            .Select(x => new { x.StorageKey, x.InvoiceNumber }).SingleOrDefaultAsync(cancellationToken);
        if (invoice is null) return null;
        var stream = await invoiceStorage.OpenReadAsync(invoice.StorageKey, cancellationToken);
        return stream is null ? null : new InvoiceFile(stream, $"{invoice.InvoiceNumber}.pdf");
    }
}
