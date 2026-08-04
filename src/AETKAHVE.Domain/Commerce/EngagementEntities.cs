using AETKAHVE.Domain.Common;

namespace AETKAHVE.Domain.Commerce;

public sealed class ReturnRequest : CommerceEntity, IConcurrencyTracked
{
    public Guid OrderId { get; set; }
    public Guid UserId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ReturnStatus Status { get; set; } = ReturnStatus.Pending;
    public DateTimeOffset RequestedAtUtc { get; set; }
    public DateTimeOffset? ReviewedAtUtc { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public string? AdminResponse { get; set; }
    public decimal RefundAmount { get; set; }
    public bool RestockApproved { get; set; }
    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();
    public Order Order { get; set; } = null!;
    public List<ReturnItem> Items { get; set; } = [];
}

public sealed class ReturnItem : CommerceEntity
{
    public Guid ReturnRequestId { get; set; }
    public Guid OrderItemId { get; set; }
    public int Quantity { get; set; }
    public string Reason { get; set; } = string.Empty;
    public ReturnItemCondition Condition { get; set; }
    public string? ImageStorageKey { get; set; }
    public ReturnRequest ReturnRequest { get; set; } = null!;
    public OrderItem OrderItem { get; set; } = null!;
}

public sealed class Review : SoftDeletableCommerceEntity
{
    public Guid UserId { get; set; }
    public Guid ProductId { get; set; }
    public Guid OrderItemId { get; set; }
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public ReviewStatus Status { get; set; } = ReviewStatus.Pending;
    public string? AdminResponse { get; set; }
    public Product Product { get; set; } = null!;
    public OrderItem OrderItem { get; set; } = null!;
}

public sealed class Notification : CommerceEntity
{
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? RelatedEntityType { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public bool IsRead { get; set; }
    public DateTimeOffset? ReadAtUtc { get; set; }
}

public sealed class NotificationDelivery : CommerceEntity, IConcurrencyTracked
{
    public Guid? NotificationId { get; set; }
    public Guid UserId { get; set; }
    public NotificationChannel Channel { get; set; }
    public string Destination { get; set; } = string.Empty;
    public string TemplateKey { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public DeliveryStatus Status { get; set; } = DeliveryStatus.Pending;
    public int AttemptCount { get; set; }
    public DateTimeOffset? NextAttemptAtUtc { get; set; }
    public DateTimeOffset? DeliveredAtUtc { get; set; }
    public string? LastError { get; set; }
    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();
    public Notification? Notification { get; set; }
}

public sealed class ContactMessage : CommerceEntity
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool PrivacyAccepted { get; set; }
    public ContactMessageStatus Status { get; set; } = ContactMessageStatus.New;
    public Guid? AnsweredByUserId { get; set; }
    public DateTimeOffset? AnsweredAtUtc { get; set; }
}
