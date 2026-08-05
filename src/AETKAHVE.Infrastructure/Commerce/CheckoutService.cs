using System.Text.Json;
using AETKAHVE.Application.Commerce;
using AETKAHVE.Domain.Common;
using AETKAHVE.Domain.Commerce;
using AETKAHVE.Infrastructure.Options;
using AETKAHVE.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AETKAHVE.Infrastructure.Commerce;

public sealed class CheckoutService(
    AppDbContext dbContext,
    IDiscountEngine discountEngine,
    IInventoryService inventoryService,
    IEnumerable<IPaymentGateway> paymentGateways,
    IInvoicePdfGenerator invoicePdfGenerator,
    IInvoiceStorage invoiceStorage,
    INotificationQueue notificationQueue,
    IOptions<PaymentOptions> paymentOptions,
    IOptions<InvoiceOptions> invoiceOptions,
    TimeProvider timeProvider) : ICheckoutService
{
    private readonly PaymentOptions _paymentOptions = paymentOptions.Value;
    private readonly InvoiceOptions _invoiceOptions = invoiceOptions.Value;

    public async Task<CheckoutInitializationResult> InitializeAsync(CheckoutRequest request, string callbackUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Length > 100) throw new CommerceRuleException("Checkout idempotency key is invalid.");
        var existing = await dbContext.Orders.AsNoTracking().Include(x => x.Payments)
            .SingleOrDefaultAsync(x => x.UserId == request.UserId && x.IdempotencyKey == request.IdempotencyKey, cancellationToken);
        if (existing is not null)
        {
            var existingPayment = existing.Payments.OrderByDescending(x => x.CreatedAtUtc).First();
            return new CheckoutInitializationResult(existing.Id, existing.OrderNumber, existingPayment.Id, existingPayment.Provider,
                existingPayment.RequestReference ?? string.Empty, existingPayment.Amount, existingPayment.Currency, callbackUrl);
        }

        var cart = await dbContext.Carts.Include(x => x.Items).ThenInclude(x => x.Product).ThenInclude(x => x.Images)
            .Include(x => x.Items).ThenInclude(x => x.ProductVariant)
            .SingleOrDefaultAsync(x => x.Id == request.CartId && x.UserId == request.UserId, cancellationToken)
            ?? throw new CommerceRuleException("Cart was not found.");
        var summary = await discountEngine.PriceAsync(cart, request.UserId, cancellationToken);
        if (summary.Items.Count == 0) throw new CommerceRuleException("Cart is empty.");
        if (summary.Warnings.Count > 0) throw new CommerceRuleException(summary.Warnings[0]);

        var addressIds = new[] { request.ShippingAddressId, request.BillingAddressId };
        var addresses = await dbContext.Addresses.AsNoTracking().Where(x => x.UserId == request.UserId && addressIds.Contains(x.Id)).ToListAsync(cancellationToken);
        var shipping = addresses.SingleOrDefault(x => x.Id == request.ShippingAddressId) ?? throw new CommerceRuleException("Shipping address was not found.");
        var billing = addresses.SingleOrDefault(x => x.Id == request.BillingAddressId) ?? throw new CommerceRuleException("Billing address was not found.");
        var now = timeProvider.GetUtcNow();
        var order = new Order
        {
            OrderNumber = CreateOrderNumber(now),
            UserId = request.UserId,
            BillingAddressSnapshot = SerializeAddress(billing),
            ShippingAddressSnapshot = SerializeAddress(shipping),
            Subtotal = summary.Subtotal,
            DiscountTotal = summary.DiscountTotal,
            TaxTotal = summary.TaxTotal,
            ShippingTotal = summary.ShippingTotal,
            GrandTotal = summary.GrandTotal,
            Currency = summary.Currency,
            CustomerNote = request.CustomerNote?.Trim(),
            IdempotencyKey = request.IdempotencyKey,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        foreach (var line in summary.Items)
        {
            order.Items.Add(new OrderItem
            {
                ProductId = line.ProductId,
                ProductVariantId = line.VariantId,
                ProductName = line.ProductName,
                Sku = line.Sku,
                VariantName = line.VariantName,
                UnitPrice = line.UnitPrice,
                DiscountAmount = line.DiscountAmount,
                TaxRate = cart.Items.Single(x => x.Id == line.ItemId).Product.TaxRate,
                TaxAmount = line.TaxAmount,
                Quantity = line.Quantity,
                LineTotal = line.LineTotal,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            });
        }
        order.StatusHistory.Add(new OrderStatusHistory
        {
            PreviousStatus = OrderStatus.PendingPayment,
            NewStatus = OrderStatus.PendingPayment,
            Description = "Checkout initialized.",
            ChangedByUserId = request.UserId,
            ChangedAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        });

        var gateway = ResolveGateway(_paymentOptions.Provider);
        var payment = new Payment
        {
            Order = order,
            Provider = gateway.ProviderName,
            IdempotencyKey = request.IdempotencyKey,
            Amount = order.GrandTotal,
            Currency = order.Currency,
            Status = PaymentStatus.Pending,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        dbContext.Orders.Add(order);
        dbContext.Payments.Add(payment);
        if (!string.IsNullOrWhiteSpace(cart.CouponCode))
        {
            var coupon = await dbContext.Coupons.SingleAsync(x => x.Code == cart.CouponCode, cancellationToken);
            dbContext.CouponUsages.Add(new CouponUsage { Coupon = coupon, UserId = request.UserId, Order = order, Status = CouponUsageStatus.Reserved, CreatedAtUtc = now, UpdatedAtUtc = now });
        }
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();
            var concurrent = await dbContext.Orders.AsNoTracking().Include(x => x.Payments)
                .SingleOrDefaultAsync(x => x.UserId == request.UserId && x.IdempotencyKey == request.IdempotencyKey, cancellationToken);
            if (concurrent is null) throw;
            var concurrentPayment = concurrent.Payments.OrderByDescending(x => x.CreatedAtUtc).First();
            return new CheckoutInitializationResult(concurrent.Id, concurrent.OrderNumber, concurrentPayment.Id, concurrentPayment.Provider,
                concurrentPayment.RequestReference ?? string.Empty, concurrentPayment.Amount, concurrentPayment.Currency, callbackUrl);
        }

        var initialized = await gateway.InitializeAsync(new PaymentRequest(payment.Id, order.Id, payment.Amount, payment.Currency,
            payment.IdempotencyKey, request.PaymentScenario, callbackUrl), cancellationToken);
        payment.RequestReference = initialized.RequestReference;
        payment.ProviderResponseCode = initialized.FailureCode;
        payment.FailureReason = initialized.FailureReason;
        payment.Status = initialized.Succeeded ? PaymentStatus.Initialized : PaymentStatus.Failed;
        payment.UpdatedAtUtc = timeProvider.GetUtcNow();
        if (!initialized.Succeeded)
        {
            order.PaymentStatus = PaymentStatus.Failed;
            dbContext.OrderStatusHistory.Add(order.TransitionTo(OrderStatus.Cancelled, request.UserId, timeProvider.GetUtcNow(), "Payment initialization failed."));
            order.CancelledAtUtc = timeProvider.GetUtcNow();
            await ReleaseCouponAsync(order.Id, cancellationToken);
        }
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CheckoutInitializationResult(order.Id, order.OrderNumber, payment.Id, payment.Provider,
            initialized.RequestReference, payment.Amount, payment.Currency, callbackUrl);
    }

    public async Task<CheckoutCompletionResult> CompleteAsync(string provider, PaymentCallbackRequest request, CancellationToken cancellationToken)
    {
        var gateway = ResolveGateway(provider);
        var verification = await gateway.VerifyAsync(request, cancellationToken);
        var payment = await dbContext.Payments.Include(x => x.Order).ThenInclude(x => x.Items)
            .SingleOrDefaultAsync(x => x.Provider == gateway.ProviderName && x.RequestReference == request.RequestReference, cancellationToken)
            ?? throw new CommerceRuleException("Payment was not found.");
        var order = payment.Order;
        if (payment.Status is PaymentStatus.Succeeded or PaymentStatus.Failed or PaymentStatus.Cancelled or PaymentStatus.Refunded)
            return new CheckoutCompletionResult(order.Id, order.OrderNumber, order.Status, payment.Status, true, "Payment callback was already processed.");

        if (verification.Amount != payment.Amount || !string.Equals(verification.Currency, payment.Currency, StringComparison.OrdinalIgnoreCase))
            verification = verification with { Succeeded = false, ResponseCode = "AMOUNT_MISMATCH", FailureReason = "Payment amount or currency did not match." };

        if (!verification.Succeeded)
        {
            payment.Status = verification.Cancelled ? PaymentStatus.Cancelled : PaymentStatus.Failed;
            payment.TransactionId = verification.TransactionId;
            payment.ProviderResponseCode = verification.ResponseCode;
            payment.FailureReason = verification.FailureReason;
            payment.CompletedAtUtc = timeProvider.GetUtcNow();
            order.PaymentStatus = payment.Status;
            dbContext.OrderStatusHistory.Add(order.TransitionTo(OrderStatus.Cancelled, order.UserId, timeProvider.GetUtcNow(), verification.Cancelled ? "Payment cancelled." : "Payment failed."));
            order.CancelledAtUtc = timeProvider.GetUtcNow();
            await ReleaseCouponAsync(order.Id, cancellationToken);
            try { await dbContext.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateConcurrencyException)
            {
                dbContext.ChangeTracker.Clear();
                var concurrent = await dbContext.Payments.AsNoTracking().Include(x => x.Order).SingleAsync(x => x.Id == payment.Id, cancellationToken);
                return new CheckoutCompletionResult(concurrent.OrderId, concurrent.Order.OrderNumber, concurrent.Order.Status,
                    concurrent.Status, true, "Payment callback was already processed.");
            }
            return new CheckoutCompletionResult(order.Id, order.OrderNumber, order.Status, payment.Status, false, verification.FailureReason ?? "Payment failed.");
        }

        var duplicateTransaction = await FindTransactionAsync(gateway.ProviderName, verification.TransactionId, payment.Id, cancellationToken);
        if (duplicateTransaction is not null)
        {
            return new CheckoutCompletionResult(duplicateTransaction.Order.Id, duplicateTransaction.Order.OrderNumber,
                duplicateTransaction.Order.Status, duplicateTransaction.Status, true, "Payment transaction was already processed.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            payment.Status = PaymentStatus.Succeeded;
            payment.TransactionId = verification.TransactionId;
            payment.ProviderResponseCode = verification.ResponseCode;
            payment.FailureReason = null;
            payment.CompletedAtUtc = timeProvider.GetUtcNow();
            order.PaymentStatus = PaymentStatus.Succeeded;
            order.PaidAtUtc = timeProvider.GetUtcNow();
            await inventoryService.DeductForOrderAsync(order, order.UserId, cancellationToken);
            dbContext.OrderStatusHistory.Add(order.TransitionTo(OrderStatus.PaymentReceived, order.UserId, timeProvider.GetUtcNow(), "Payment verified."));

            var invoice = await CreateInvoiceAsync(order, cancellationToken);
            invoice.OrderId = order.Id;
            invoice.Order = order;
            order.Invoice = invoice;
            dbContext.Invoices.Add(invoice);
            var shipment = new Shipment
            {
                OrderId = order.Id,
                Order = order,
                ShippingCompany = "Pending",
                Status = ShipmentStatus.Pending,
                CreatedAtUtc = timeProvider.GetUtcNow(),
                UpdatedAtUtc = timeProvider.GetUtcNow(),
            };
            order.Shipment = shipment;
            dbContext.Shipments.Add(shipment);
            var usage = await dbContext.CouponUsages.SingleOrDefaultAsync(x => x.OrderId == order.Id, cancellationToken);
            if (usage is not null) usage.Status = CouponUsageStatus.Consumed;
            var cart = await dbContext.Carts.Include(x => x.Items).SingleOrDefaultAsync(x => x.UserId == order.UserId, cancellationToken);
            if (cart is not null) { dbContext.CartItems.RemoveRange(cart.Items); cart.Items.Clear(); cart.CouponCode = null; cart.UpdatedAtUtc = timeProvider.GetUtcNow(); }
            await notificationQueue.EnqueueOrderAsync(order, "OrderPaid", cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new CheckoutCompletionResult(order.Id, order.OrderNumber, order.Status, payment.Status, false, "Payment completed and order created.");
        }
        catch (Exception exception) when (exception is CommerceRuleException or DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            var currentPayment = await dbContext.Payments.AsNoTracking().Include(x => x.Order)
                .SingleAsync(x => x.Id == payment.Id, cancellationToken);
            if (currentPayment.Status is PaymentStatus.Succeeded or PaymentStatus.Failed or PaymentStatus.Cancelled or PaymentStatus.Refunded)
            {
                return new CheckoutCompletionResult(currentPayment.OrderId, currentPayment.Order.OrderNumber,
                    currentPayment.Order.Status, currentPayment.Status, true, "Payment callback was already processed.");
            }
            duplicateTransaction = await FindTransactionAsync(gateway.ProviderName, verification.TransactionId, payment.Id, cancellationToken);
            if (duplicateTransaction is not null)
            {
                return new CheckoutCompletionResult(duplicateTransaction.Order.Id, duplicateTransaction.Order.OrderNumber,
                    duplicateTransaction.Order.Status, duplicateTransaction.Status, true, "Payment transaction was already processed.");
            }
            return await RefundAfterInventoryFailureAsync(gateway, payment.Id, verification, exception.Message, cancellationToken);
        }
    }

    private async Task<CheckoutCompletionResult> RefundAfterInventoryFailureAsync(IPaymentGateway gateway, Guid paymentId, PaymentVerificationResult verification, string reason, CancellationToken cancellationToken)
    {
        var payment = await dbContext.Payments.Include(x => x.Order).SingleAsync(x => x.Id == paymentId, cancellationToken);
        var refundResult = await gateway.RefundAsync(new RefundRequest(payment.Id, verification.TransactionId, payment.Amount, payment.Currency, "stock-failure"), cancellationToken);
        var now = timeProvider.GetUtcNow();
        payment.TransactionId = verification.TransactionId;
        payment.ProviderResponseCode = verification.ResponseCode;
        payment.CompletedAtUtc = now;
        payment.Status = refundResult.Succeeded ? PaymentStatus.Refunded : PaymentStatus.Succeeded;
        payment.Order.PaymentStatus = payment.Status;
        dbContext.OrderStatusHistory.Add(payment.Order.TransitionTo(OrderStatus.Cancelled, null, now, "Inventory could not be allocated after payment verification."));
        payment.Order.CancelledAtUtc = now;
        dbContext.Refunds.Add(new Refund
        {
            PaymentId = payment.Id,
            Amount = payment.Amount,
            Status = refundResult.Succeeded ? RefundStatus.Succeeded : RefundStatus.Failed,
            ProviderReference = refundResult.ProviderReference,
            FailureReason = refundResult.FailureReason ?? reason,
            CompletedAtUtc = refundResult.Succeeded ? now : null,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        });
        await ReleaseCouponAsync(payment.OrderId, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new CheckoutCompletionResult(payment.OrderId, payment.Order.OrderNumber, payment.Order.Status, payment.Status, false,
            refundResult.Succeeded ? "Stock was unavailable; payment was refunded." : "Stock was unavailable; refund requires retry.");
    }

    private async Task<Invoice> CreateInvoiceAsync(Order order, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.AsNoTracking().Where(x => x.Id == order.UserId).Select(x => new { x.FirstName, x.LastName }).SingleAsync(cancellationToken);
        var invoiceNumber = $"ARL-{timeProvider.GetUtcNow():yyyyMMdd}-{order.Id.ToString("N")[..10].ToUpperInvariant()}";
        var document = new InvoiceDocument(invoiceNumber, timeProvider.GetUtcNow(), _invoiceOptions.SellerName, order.OrderNumber,
            $"{user.FirstName} {user.LastName}", order.BillingAddressSnapshot,
            order.Items.Select(x => new InvoiceLine(x.ProductName, x.Sku, x.Quantity, x.UnitPrice, x.DiscountAmount, x.TaxAmount, x.LineTotal)).ToList(),
            order.Subtotal, order.DiscountTotal, order.TaxTotal, order.ShippingTotal, order.GrandTotal, order.Currency);
        var bytes = await invoicePdfGenerator.GenerateAsync(document, cancellationToken);
        var storageKey = await invoiceStorage.SaveAsync(invoiceNumber, bytes, cancellationToken);
        return new Invoice { InvoiceNumber = invoiceNumber, InvoiceDateUtc = document.InvoiceDateUtc, StorageKey = storageKey, GrandTotal = order.GrandTotal, Currency = order.Currency, CreatedAtUtc = document.InvoiceDateUtc, UpdatedAtUtc = document.InvoiceDateUtc };
    }

    private Task ReleaseCouponAsync(Guid orderId, CancellationToken cancellationToken) =>
        dbContext.CouponUsages.Where(x => x.OrderId == orderId).ExecuteUpdateAsync(x => x.SetProperty(y => y.Status, CouponUsageStatus.Released), cancellationToken);

    private Task<Payment?> FindTransactionAsync(string provider, string transactionId, Guid paymentId, CancellationToken cancellationToken) =>
        string.IsNullOrWhiteSpace(transactionId)
            ? Task.FromResult<Payment?>(null)
            : dbContext.Payments.AsNoTracking().Include(x => x.Order)
                .SingleOrDefaultAsync(x => x.Id != paymentId && x.Provider == provider && x.TransactionId == transactionId, cancellationToken);

    private IPaymentGateway ResolveGateway(string provider) => paymentGateways.SingleOrDefault(x => x.ProviderName.Equals(provider, StringComparison.OrdinalIgnoreCase))
        ?? throw new CommerceRuleException("Payment provider is not configured.");
    private static string SerializeAddress(Address address) => JsonSerializer.Serialize(new { address.FirstName, address.LastName, address.PhoneNumber, address.Country, address.City, address.District, address.Neighborhood, address.PostalCode, address.AddressLine });
    private static string CreateOrderNumber(DateTimeOffset now) => $"ARL-{now:yyyyMMdd}-{Convert.ToHexString(Guid.NewGuid().ToByteArray())[..12]}";
}
