using AETKAHVE.Domain.Commerce;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AETKAHVE.Infrastructure.Persistence.Configurations;

internal sealed class ReturnRequestConfiguration : CommerceEntityConfiguration<ReturnRequest>
{
    protected override void ConfigureEntity(EntityTypeBuilder<ReturnRequest> builder)
    {
        builder.Property(x => x.Reason).HasMaxLength(250).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.AdminResponse).HasMaxLength(2000);
        builder.Property(x => x.RefundAmount).Money();
        builder.Property(x => x.ConcurrencyToken).IsConcurrencyToken();
        builder.HasIndex(x => new { x.UserId, x.OrderId });
        builder.HasOne(x => x.Order).WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ReturnItemConfiguration : CommerceEntityConfiguration<ReturnItem>
{
    protected override void ConfigureEntity(EntityTypeBuilder<ReturnItem> builder)
    {
        builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Condition).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.ImageStorageKey).HasMaxLength(512);
        builder.HasOne(x => x.ReturnRequest).WithMany(x => x.Items).HasForeignKey(x => x.ReturnRequestId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.OrderItem).WithMany().HasForeignKey(x => x.OrderItemId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ReviewConfiguration : CommerceEntityConfiguration<Review>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Review> builder)
    {
        builder.Property(x => x.Comment).HasMaxLength(3000).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.AdminResponse).HasMaxLength(1000);
        builder.HasIndex(x => new { x.UserId, x.OrderItemId }).IsUnique();
        builder.HasQueryFilter(x => x.DeletedAtUtc == null);
        builder.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.OrderItem).WithMany().HasForeignKey(x => x.OrderItemId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class NotificationConfiguration : CommerceEntityConfiguration<Notification>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Notification> builder)
    {
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.Type).HasMaxLength(80).IsRequired();
        builder.Property(x => x.RelatedEntityType).HasMaxLength(80);
        builder.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAtUtc });
    }
}

internal sealed class NotificationDeliveryConfiguration : CommerceEntityConfiguration<NotificationDelivery>
{
    protected override void ConfigureEntity(EntityTypeBuilder<NotificationDelivery> builder)
    {
        builder.Property(x => x.Channel).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Destination).HasMaxLength(320).IsRequired();
        builder.Property(x => x.TemplateKey).HasMaxLength(100).IsRequired();
        builder.Property(x => x.PayloadJson).HasMaxLength(8000).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.LastError).HasMaxLength(500);
        builder.Property(x => x.ConcurrencyToken).IsConcurrencyToken();
        builder.HasIndex(x => new { x.Status, x.NextAttemptAtUtc });
        builder.HasOne(x => x.Notification).WithMany().HasForeignKey(x => x.NotificationId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class ContactMessageConfiguration : CommerceEntityConfiguration<ContactMessage>
{
    protected override void ConfigureEntity(EntityTypeBuilder<ContactMessage> builder)
    {
        builder.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(320).IsRequired();
        builder.Property(x => x.PhoneNumber).HasMaxLength(30);
        builder.Property(x => x.Subject).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(5000).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        builder.HasIndex(x => new { x.Status, x.CreatedAtUtc });
    }
}
