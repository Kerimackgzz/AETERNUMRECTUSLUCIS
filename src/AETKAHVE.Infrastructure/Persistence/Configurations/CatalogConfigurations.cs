using AETKAHVE.Domain.Commerce;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AETKAHVE.Infrastructure.Persistence.Configurations;

internal abstract class LookupConfiguration<T> : CommerceEntityConfiguration<T> where T : CatalogLookup
{
    protected override void ConfigureEntity(EntityTypeBuilder<T> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Slug).HasMaxLength(160).IsRequired();
        builder.HasIndex(x => x.Slug).IsUnique();
        builder.HasQueryFilter(x => x.DeletedAtUtc == null);
        ConfigureLookup(builder);
    }

    protected virtual void ConfigureLookup(EntityTypeBuilder<T> builder)
    {
    }
}

internal sealed class CategoryConfiguration : LookupConfiguration<Category>;
internal sealed class BrandConfiguration : LookupConfiguration<Brand>;
internal sealed class CoffeeTypeConfiguration : LookupConfiguration<CoffeeType>;
internal sealed class BeanTypeConfiguration : LookupConfiguration<BeanType>;
internal sealed class RoastLevelConfiguration : LookupConfiguration<RoastLevel>;

internal sealed class OriginConfiguration : LookupConfiguration<Origin>
{
    protected override void ConfigureLookup(EntityTypeBuilder<Origin> builder) =>
        builder.Property(x => x.CountryCode).HasMaxLength(2);
}

internal sealed class ProductConfiguration : CommerceEntityConfiguration<Product>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Product> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Slug).HasMaxLength(220).IsRequired();
        builder.Property(x => x.Sku).HasMaxLength(80).IsRequired();
        builder.Property(x => x.ShortDescription).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(8000).IsRequired();
        builder.Property(x => x.BasePrice).Money();
        builder.Property(x => x.DiscountedPrice).Money();
        builder.Property(x => x.TaxRate).Rate();
        builder.Property(x => x.ConcurrencyToken).IsConcurrencyToken();
        builder.HasIndex(x => x.Slug).IsUnique();
        builder.HasIndex(x => x.Sku).IsUnique();
        builder.HasIndex(x => new { x.IsActive, x.IsFeatured });
        builder.HasQueryFilter(x => x.DeletedAtUtc == null);
        builder.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Brand).WithMany().HasForeignKey(x => x.BrandId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.CoffeeType).WithMany().HasForeignKey(x => x.CoffeeTypeId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.BeanType).WithMany().HasForeignKey(x => x.BeanTypeId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.RoastLevel).WithMany().HasForeignKey(x => x.RoastLevelId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.Origin).WithMany().HasForeignKey(x => x.OriginId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class ProductImageConfiguration : CommerceEntityConfiguration<ProductImage>
{
    protected override void ConfigureEntity(EntityTypeBuilder<ProductImage> builder)
    {
        builder.Property(x => x.StorageKey).HasMaxLength(512).IsRequired();
        builder.Property(x => x.AltText).HasMaxLength(250).IsRequired();
        builder.HasIndex(x => new { x.ProductId, x.DisplayOrder });
        builder.HasQueryFilter(x => x.Product.DeletedAtUtc == null);
        builder.HasOne(x => x.Product).WithMany(x => x.Images).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ProductVariantConfiguration : CommerceEntityConfiguration<ProductVariant>
{
    protected override void ConfigureEntity(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.Property(x => x.Weight).HasPrecision(10, 2);
        builder.Property(x => x.Unit).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Sku).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Price).Money();
        builder.Property(x => x.DiscountedPrice).Money();
        builder.Property(x => x.ConcurrencyToken).IsConcurrencyToken();
        builder.HasIndex(x => x.Sku).IsUnique();
        builder.HasQueryFilter(x => x.DeletedAtUtc == null);
        builder.HasOne(x => x.Product).WithMany(x => x.Variants).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
    }
}
