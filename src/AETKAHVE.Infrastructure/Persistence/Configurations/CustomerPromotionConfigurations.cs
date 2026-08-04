using AETKAHVE.Domain.Commerce;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AETKAHVE.Infrastructure.Persistence.Configurations;

internal sealed class AddressConfiguration : CommerceEntityConfiguration<Address>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Address> builder)
    {
        builder.Property(x => x.Title).HasMaxLength(80).IsRequired();
        builder.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.LastName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.PhoneNumber).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Country).HasMaxLength(100).IsRequired();
        builder.Property(x => x.City).HasMaxLength(100).IsRequired();
        builder.Property(x => x.District).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Neighborhood).HasMaxLength(150);
        builder.Property(x => x.PostalCode).HasMaxLength(20);
        builder.Property(x => x.AddressLine).HasMaxLength(1000).IsRequired();
        builder.HasIndex(x => x.UserId);
        builder.HasQueryFilter(x => x.DeletedAtUtc == null);
    }
}

internal sealed class CartConfiguration : CommerceEntityConfiguration<Cart>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Cart> builder)
    {
        builder.Property(x => x.CouponCode).HasMaxLength(80);
        builder.HasIndex(x => x.UserId).IsUnique().HasFilter("[UserId] IS NOT NULL");
        builder.HasIndex(x => x.GuestToken).IsUnique().HasFilter("[GuestToken] IS NOT NULL");
    }
}

internal sealed class CartItemConfiguration : CommerceEntityConfiguration<CartItem>
{
    protected override void ConfigureEntity(EntityTypeBuilder<CartItem> builder)
    {
        builder.HasIndex(x => new { x.CartId, x.ProductId }).IsUnique().HasFilter("[ProductVariantId] IS NULL");
        builder.HasIndex(x => new { x.CartId, x.ProductId, x.ProductVariantId }).IsUnique().HasFilter("[ProductVariantId] IS NOT NULL");
        builder.HasOne(x => x.Cart).WithMany(x => x.Items).HasForeignKey(x => x.CartId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ProductVariant).WithMany().HasForeignKey(x => x.ProductVariantId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class FavoriteConfiguration : CommerceEntityConfiguration<Favorite>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Favorite> builder)
    {
        builder.HasIndex(x => new { x.UserId, x.ProductId }).IsUnique();
        builder.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CampaignConfiguration : CommerceEntityConfiguration<Campaign>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Campaign> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(180).IsRequired();
        builder.Property(x => x.Slug).HasMaxLength(200).IsRequired();
        builder.Property(x => x.DiscountType).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.DiscountValue).Money();
        builder.Property(x => x.MinimumCartAmount).Money();
        builder.Property(x => x.MaximumDiscountAmount).Money();
        builder.HasIndex(x => x.Slug).IsUnique();
        builder.HasIndex(x => new { x.IsActive, x.StartDateUtc, x.EndDateUtc });
    }
}

internal sealed class CampaignProductConfiguration : IEntityTypeConfiguration<CampaignProduct>
{
    public void Configure(EntityTypeBuilder<CampaignProduct> builder)
    {
        builder.HasKey(x => new { x.CampaignId, x.ProductId });
        builder.HasOne(x => x.Campaign).WithMany(x => x.Products).HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CampaignCategoryConfiguration : IEntityTypeConfiguration<CampaignCategory>
{
    public void Configure(EntityTypeBuilder<CampaignCategory> builder)
    {
        builder.HasKey(x => new { x.CampaignId, x.CategoryId });
        builder.HasOne(x => x.Campaign).WithMany(x => x.Categories).HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CouponConfiguration : CommerceEntityConfiguration<Coupon>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Coupon> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(180).IsRequired();
        builder.Property(x => x.Code).HasMaxLength(80).IsRequired();
        builder.Property(x => x.DiscountType).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.DiscountValue).Money();
        builder.Property(x => x.MinimumCartAmount).Money();
        builder.Property(x => x.MaximumDiscountAmount).Money();
        builder.HasIndex(x => x.Code).IsUnique();
    }
}

internal sealed class CouponUsageConfiguration : CommerceEntityConfiguration<CouponUsage>
{
    protected override void ConfigureEntity(EntityTypeBuilder<CouponUsage> builder)
    {
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        builder.HasIndex(x => new { x.CouponId, x.UserId, x.OrderId }).IsUnique();
        builder.HasOne(x => x.Coupon).WithMany(x => x.Usages).HasForeignKey(x => x.CouponId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Order).WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
    }
}
