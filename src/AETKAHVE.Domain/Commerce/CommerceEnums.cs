namespace AETKAHVE.Domain.Commerce;

public enum DiscountType { Percentage, FixedAmount, FreeShipping }
public enum OrderStatus { PendingPayment, PaymentReceived, Preparing, Packed, Shipped, OutForDelivery, Delivered, Cancelled, ReturnRequested, Returned, Refunded }
public enum PaymentStatus { Pending, Initialized, Succeeded, Failed, Cancelled, Refunded }
public enum RefundStatus { Pending, Succeeded, Failed }
public enum ShipmentStatus { Pending, Created, Shipped, OutForDelivery, Delivered, Cancelled }
public enum ReturnStatus { Pending, UnderReview, Approved, Rejected, AwaitingProduct, ProductReceived, RefundPending, Completed, Cancelled }
public enum ReviewStatus { Pending, Approved, Rejected }
public enum StockMovementType { Purchase, Sale, Cancellation, Return, ManualIncrease, ManualDecrease, Correction }
public enum NotificationChannel { InApp, Email, Sms }
public enum DeliveryStatus { Pending, Processing, Delivered, Failed }
public enum ContactMessageStatus { New, InProgress, Answered, Closed }
public enum CouponUsageStatus { Reserved, Consumed, Released }
public enum WeightUnit { Gram, Kilogram }
public enum ReturnItemCondition { Unopened, Opened, Damaged, Defective }

public static class OrderStatusRules
{
    private static readonly IReadOnlyDictionary<OrderStatus, OrderStatus[]> AllowedTransitions =
        new Dictionary<OrderStatus, OrderStatus[]>
        {
            [OrderStatus.PendingPayment] = [OrderStatus.PaymentReceived, OrderStatus.Cancelled],
            [OrderStatus.PaymentReceived] = [OrderStatus.Preparing, OrderStatus.Cancelled],
            [OrderStatus.Preparing] = [OrderStatus.Packed, OrderStatus.Cancelled],
            [OrderStatus.Packed] = [OrderStatus.Shipped, OrderStatus.Cancelled],
            [OrderStatus.Shipped] = [OrderStatus.OutForDelivery, OrderStatus.Delivered],
            [OrderStatus.OutForDelivery] = [OrderStatus.Delivered],
            [OrderStatus.Delivered] = [OrderStatus.ReturnRequested],
            [OrderStatus.ReturnRequested] = [OrderStatus.Returned],
            [OrderStatus.Returned] = [OrderStatus.Refunded],
        };

    public static bool CanTransition(OrderStatus current, OrderStatus next) =>
        AllowedTransitions.TryGetValue(current, out var allowed) && allowed.Contains(next);

    public static IReadOnlyList<OrderStatus> GetAllowedNext(OrderStatus current) =>
        AllowedTransitions.TryGetValue(current, out var allowed) ? allowed : [];
}

public static class ReturnStatusRules
{
    private static readonly IReadOnlyDictionary<ReturnStatus, ReturnStatus[]> AllowedTransitions =
        new Dictionary<ReturnStatus, ReturnStatus[]>
        {
            [ReturnStatus.Pending] = [ReturnStatus.UnderReview, ReturnStatus.Approved, ReturnStatus.Rejected, ReturnStatus.Cancelled],
            [ReturnStatus.UnderReview] = [ReturnStatus.Approved, ReturnStatus.Rejected],
            [ReturnStatus.Approved] = [ReturnStatus.AwaitingProduct, ReturnStatus.ProductReceived],
            [ReturnStatus.AwaitingProduct] = [ReturnStatus.ProductReceived],
            [ReturnStatus.ProductReceived] = [ReturnStatus.RefundPending, ReturnStatus.Completed],
            [ReturnStatus.RefundPending] = [ReturnStatus.Completed],
        };

    public static bool CanTransition(ReturnStatus current, ReturnStatus next) =>
        AllowedTransitions.TryGetValue(current, out var allowed) && allowed.Contains(next);

    public static IReadOnlyList<ReturnStatus> GetAllowedNext(ReturnStatus current) =>
        AllowedTransitions.TryGetValue(current, out var allowed) ? allowed : [];
}
