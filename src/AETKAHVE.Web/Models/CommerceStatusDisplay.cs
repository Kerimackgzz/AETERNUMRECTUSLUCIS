using AETKAHVE.Domain.Commerce;

namespace AETKAHVE.Web.Models;

public static class CommerceStatusDisplay
{
    public static (string Label, string Kind) OrderStatusInfo(OrderStatus status) => status switch
    {
        OrderStatus.PendingPayment => ("Ödeme Bekleniyor", "warning"),
        OrderStatus.PaymentReceived => ("Ödeme Alındı", "info"),
        OrderStatus.Preparing => ("Hazırlanıyor", "info"),
        OrderStatus.Packed => ("Paketlendi", "info"),
        OrderStatus.Shipped => ("Kargoya Verildi", "info"),
        OrderStatus.OutForDelivery => ("Dağıtımda", "info"),
        OrderStatus.Delivered => ("Teslim Edildi", "success"),
        OrderStatus.Cancelled => ("İptal Edildi", "error"),
        OrderStatus.ReturnRequested => ("İade Talep Edildi", "warning"),
        OrderStatus.Returned => ("İade Edildi", "warning"),
        OrderStatus.Refunded => ("Ücret İadesi Yapıldı", "success"),
        _ => (status.ToString(), "info"),
    };

    public static (string Label, string Kind) PaymentStatusInfo(PaymentStatus status) => status switch
    {
        PaymentStatus.Pending => ("Bekliyor", "warning"),
        PaymentStatus.Initialized => ("Başlatıldı", "info"),
        PaymentStatus.Succeeded => ("Ödendi", "success"),
        PaymentStatus.Failed => ("Başarısız", "error"),
        PaymentStatus.Cancelled => ("İptal Edildi", "error"),
        PaymentStatus.Refunded => ("İade Edildi", "warning"),
        _ => (status.ToString(), "info"),
    };

    public static (string Label, string Kind) ShipmentStatusInfo(ShipmentStatus status) => status switch
    {
        ShipmentStatus.Pending => ("Bekliyor", "warning"),
        ShipmentStatus.Created => ("Kargo Oluşturuldu", "info"),
        ShipmentStatus.Shipped => ("Kargoya Verildi", "info"),
        ShipmentStatus.OutForDelivery => ("Dağıtımda", "info"),
        ShipmentStatus.Delivered => ("Teslim Edildi", "success"),
        ShipmentStatus.Cancelled => ("İptal Edildi", "error"),
        _ => (status.ToString(), "info"),
    };

    public static (string Label, string Kind) ReturnStatusInfo(ReturnStatus status) => status switch
    {
        ReturnStatus.Pending => ("Beklemede", "warning"),
        ReturnStatus.UnderReview => ("İnceleniyor", "info"),
        ReturnStatus.Approved => ("Onaylandı", "success"),
        ReturnStatus.Rejected => ("Reddedildi", "error"),
        ReturnStatus.AwaitingProduct => ("Ürün Bekleniyor", "warning"),
        ReturnStatus.ProductReceived => ("Ürün Alındı", "info"),
        ReturnStatus.RefundPending => ("İade Bekliyor", "warning"),
        ReturnStatus.Completed => ("Tamamlandı", "success"),
        ReturnStatus.Cancelled => ("İptal Edildi", "error"),
        _ => (status.ToString(), "info"),
    };

    public static (string Label, string Kind) ReviewStatusInfo(ReviewStatus status) => status switch
    {
        ReviewStatus.Pending => ("İncelemede", "warning"),
        ReviewStatus.Approved => ("Yayında", "success"),
        ReviewStatus.Rejected => ("Reddedildi", "error"),
        _ => (status.ToString(), "info"),
    };

    public static (string Label, string Kind) ContactMessageStatusInfo(ContactMessageStatus status) => status switch
    {
        ContactMessageStatus.New => ("Yeni", "info"),
        ContactMessageStatus.InProgress => ("İşlemde", "warning"),
        ContactMessageStatus.Answered => ("Yanıtlandı", "success"),
        ContactMessageStatus.Closed => ("Kapatıldı", "info"),
        _ => (status.ToString(), "info"),
    };
}
