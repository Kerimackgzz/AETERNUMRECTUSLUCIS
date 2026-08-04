using AETKAHVE.Domain.Common;

namespace AETKAHVE.Domain.Commerce;

public sealed class Order : CommerceEntity, IConcurrencyTracked
{
    public string OrderNumber { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.PendingPayment;
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;
    public ShipmentStatus ShippingStatus { get; set; } = ShipmentStatus.Pending;
    public string BillingAddressSnapshot { get; set; } = string.Empty;
    public string ShippingAddressSnapshot { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal ShippingTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public string Currency { get; set; } = "TRY";
    public string? CustomerNote { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTimeOffset? PaidAtUtc { get; set; }
    public DateTimeOffset? ShippedAtUtc { get; set; }
    public DateTimeOffset? DeliveredAtUtc { get; set; }
    public DateTimeOffset? CancelledAtUtc { get; set; }
    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();
    public List<OrderItem> Items { get; set; } = [];
    public List<OrderStatusHistory> StatusHistory { get; set; } = [];
    public List<Payment> Payments { get; set; } = [];
    public Shipment? Shipment { get; set; }
    public Invoice? Invoice { get; set; }

    public OrderStatusHistory TransitionTo(OrderStatus next, Guid? changedByUserId, DateTimeOffset changedAtUtc, string description)
    {
        if (!OrderStatusRules.CanTransition(Status, next))
        {
            throw new CommerceRuleException($"Order cannot transition from {Status} to {next}.");
        }

        var history = new OrderStatusHistory
        {
            OrderId = Id,
            PreviousStatus = Status,
            NewStatus = next,
            Description = description,
            ChangedByUserId = changedByUserId,
            ChangedAtUtc = changedAtUtc,
            CreatedAtUtc = changedAtUtc,
            UpdatedAtUtc = changedAtUtc,
        };
        StatusHistory.Add(history);
        Status = next;
        UpdatedAtUtc = changedAtUtc;
        ConcurrencyToken = Guid.NewGuid();
        return history;
    }
}

public sealed class OrderItem : CommerceEntity
{
    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? VariantName { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
    public Order Order { get; set; } = null!;
}

public sealed class OrderStatusHistory : CommerceEntity
{
    public Guid OrderId { get; set; }
    public OrderStatus PreviousStatus { get; set; }
    public OrderStatus NewStatus { get; set; }
    public string Description { get; set; } = string.Empty;
    public Guid? ChangedByUserId { get; set; }
    public DateTimeOffset ChangedAtUtc { get; set; }
    public Order Order { get; set; } = null!;
}

public sealed class Payment : CommerceEntity, IConcurrencyTracked
{
    public Guid OrderId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string? TransactionId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "TRY";
    public PaymentStatus Status { get; set; }
    public string? RequestReference { get; set; }
    public string? ProviderResponseCode { get; set; }
    public string? FailureReason { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();
    public Order Order { get; set; } = null!;
    public List<Refund> Refunds { get; set; } = [];
}

public sealed class Refund : CommerceEntity, IConcurrencyTracked
{
    public Guid PaymentId { get; set; }
    public Guid? ReturnRequestId { get; set; }
    public decimal Amount { get; set; }
    public RefundStatus Status { get; set; }
    public string? ProviderReference { get; set; }
    public string? FailureReason { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();
    public Payment Payment { get; set; } = null!;
}

public sealed class Shipment : CommerceEntity, IConcurrencyTracked
{
    public Guid OrderId { get; set; }
    public string ShippingCompany { get; set; } = string.Empty;
    public string? TrackingNumber { get; set; }
    public string? TrackingUrl { get; set; }
    public ShipmentStatus Status { get; set; } = ShipmentStatus.Pending;
    public DateTimeOffset? EstimatedDeliveryDateUtc { get; set; }
    public DateTimeOffset? ShippedAtUtc { get; set; }
    public DateTimeOffset? DeliveredAtUtc { get; set; }
    public string? ShippingNote { get; set; }
    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();
    public Order Order { get; set; } = null!;
    public List<ShipmentStatusHistory> StatusHistory { get; set; } = [];
}

public sealed class ShipmentStatusHistory : CommerceEntity
{
    public Guid ShipmentId { get; set; }
    public ShipmentStatus PreviousStatus { get; set; }
    public ShipmentStatus NewStatus { get; set; }
    public string? Description { get; set; }
    public Guid? ChangedByUserId { get; set; }
    public Shipment Shipment { get; set; } = null!;
}

public sealed class Invoice : CommerceEntity
{
    public Guid OrderId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTimeOffset InvoiceDateUtc { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public decimal GrandTotal { get; set; }
    public string Currency { get; set; } = "TRY";
    public Order Order { get; set; } = null!;
}

public sealed class StockMovement : CommerceEntity
{
    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public StockMovementType MovementType { get; set; }
    public int Quantity { get; set; }
    public int PreviousStock { get; set; }
    public int NewStock { get; set; }
    public string ReferenceType { get; set; } = string.Empty;
    public Guid ReferenceId { get; set; }
    public string Description { get; set; } = string.Empty;
    public Guid? CreatedByUserId { get; set; }
    public Product Product { get; set; } = null!;
    public ProductVariant? ProductVariant { get; set; }
}
