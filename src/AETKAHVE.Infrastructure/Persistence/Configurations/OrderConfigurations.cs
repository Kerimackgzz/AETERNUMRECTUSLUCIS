using AETKAHVE.Domain.Commerce;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AETKAHVE.Infrastructure.Persistence.Configurations;

internal sealed class OrderConfiguration : CommerceEntityConfiguration<Order>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Order> builder)
    {
        builder.Property(x => x.OrderNumber).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.PaymentStatus).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.ShippingStatus).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.BillingAddressSnapshot).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.ShippingAddressSnapshot).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.Subtotal).Money();
        builder.Property(x => x.DiscountTotal).Money();
        builder.Property(x => x.TaxTotal).Money();
        builder.Property(x => x.ShippingTotal).Money();
        builder.Property(x => x.GrandTotal).Money();
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.CustomerNote).HasMaxLength(1000);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ConcurrencyToken).IsConcurrencyToken();
        builder.HasIndex(x => x.OrderNumber).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
    }
}

internal sealed class OrderItemConfiguration : CommerceEntityConfiguration<OrderItem>
{
    protected override void ConfigureEntity(EntityTypeBuilder<OrderItem> builder)
    {
        builder.Property(x => x.ProductName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Sku).HasMaxLength(80).IsRequired();
        builder.Property(x => x.VariantName).HasMaxLength(100);
        builder.Property(x => x.UnitPrice).Money();
        builder.Property(x => x.DiscountAmount).Money();
        builder.Property(x => x.TaxRate).Rate();
        builder.Property(x => x.TaxAmount).Money();
        builder.Property(x => x.LineTotal).Money();
        builder.HasOne(x => x.Order).WithMany(x => x.Items).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class OrderStatusHistoryConfiguration : CommerceEntityConfiguration<OrderStatusHistory>
{
    protected override void ConfigureEntity(EntityTypeBuilder<OrderStatusHistory> builder)
    {
        builder.Property(x => x.PreviousStatus).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.NewStatus).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Description).HasMaxLength(500).IsRequired();
        builder.HasIndex(x => new { x.OrderId, x.ChangedAtUtc });
        builder.HasOne(x => x.Order).WithMany(x => x.StatusHistory).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class PaymentConfiguration : CommerceEntityConfiguration<Payment>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Payment> builder)
    {
        builder.Property(x => x.Provider).HasMaxLength(80).IsRequired();
        builder.Property(x => x.TransactionId).HasMaxLength(160);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Amount).Money();
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.RequestReference).HasMaxLength(160);
        builder.Property(x => x.ProviderResponseCode).HasMaxLength(80);
        builder.Property(x => x.FailureReason).HasMaxLength(500);
        builder.Property(x => x.ConcurrencyToken).IsConcurrencyToken();
        builder.HasIndex(x => new { x.Provider, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => new { x.Provider, x.TransactionId }).IsUnique().HasFilter("[TransactionId] IS NOT NULL");
        builder.HasOne(x => x.Order).WithMany(x => x.Payments).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class RefundConfiguration : CommerceEntityConfiguration<Refund>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Refund> builder)
    {
        builder.Property(x => x.Amount).Money();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.ProviderReference).HasMaxLength(160);
        builder.Property(x => x.FailureReason).HasMaxLength(500);
        builder.Property(x => x.ConcurrencyToken).IsConcurrencyToken();
        builder.HasOne(x => x.Payment).WithMany(x => x.Refunds).HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ShipmentConfiguration : CommerceEntityConfiguration<Shipment>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Shipment> builder)
    {
        builder.Property(x => x.ShippingCompany).HasMaxLength(150).IsRequired();
        builder.Property(x => x.TrackingNumber).HasMaxLength(160);
        builder.Property(x => x.TrackingUrl).HasMaxLength(1000);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.ShippingNote).HasMaxLength(1000);
        builder.Property(x => x.ConcurrencyToken).IsConcurrencyToken();
        builder.HasIndex(x => x.OrderId).IsUnique();
        builder.HasIndex(x => x.TrackingNumber);
        builder.HasOne(x => x.Order).WithOne(x => x.Shipment).HasForeignKey<Shipment>(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ShipmentStatusHistoryConfiguration : CommerceEntityConfiguration<ShipmentStatusHistory>
{
    protected override void ConfigureEntity(EntityTypeBuilder<ShipmentStatusHistory> builder)
    {
        builder.Property(x => x.PreviousStatus).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.NewStatus).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.HasOne(x => x.Shipment).WithMany(x => x.StatusHistory).HasForeignKey(x => x.ShipmentId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class InvoiceConfiguration : CommerceEntityConfiguration<Invoice>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Invoice> builder)
    {
        builder.Property(x => x.InvoiceNumber).HasMaxLength(50).IsRequired();
        builder.Property(x => x.StorageKey).HasMaxLength(512).IsRequired();
        builder.Property(x => x.GrandTotal).Money();
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.HasIndex(x => x.InvoiceNumber).IsUnique();
        builder.HasIndex(x => x.OrderId).IsUnique();
        builder.HasOne(x => x.Order).WithOne(x => x.Invoice).HasForeignKey<Invoice>(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class StockMovementConfiguration : CommerceEntityConfiguration<StockMovement>
{
    protected override void ConfigureEntity(EntityTypeBuilder<StockMovement> builder)
    {
        builder.Property(x => x.MovementType).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.ReferenceType).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500).IsRequired();
        builder.HasIndex(x => new { x.ReferenceType, x.ReferenceId, x.ProductId, x.ProductVariantId, x.MovementType }).IsUnique();
        builder.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ProductVariant).WithMany().HasForeignKey(x => x.ProductVariantId).OnDelete(DeleteBehavior.Restrict);
    }
}
