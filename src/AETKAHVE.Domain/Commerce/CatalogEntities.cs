using AETKAHVE.Domain.Common;
using System.ComponentModel.DataAnnotations.Schema;

namespace AETKAHVE.Domain.Commerce;

[NotMapped]
public abstract class CatalogLookup : SoftDeletableCommerceEntity
{
    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}

public sealed class Category : CatalogLookup;
public sealed class Brand : CatalogLookup;
public sealed class CoffeeType : CatalogLookup;
public sealed class BeanType : CatalogLookup;
public sealed class RoastLevel : CatalogLookup;

public sealed class Origin : CatalogLookup
{
    public string? CountryCode { get; set; }
}

public sealed class Product : SoftDeletableCommerceEntity, IConcurrencyTracked
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string ShortDescription { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
    public decimal? DiscountedPrice { get; set; }
    public decimal TaxRate { get; set; }
    public int StockQuantity { get; set; }
    public int CriticalStockLevel { get; set; }
    public Guid CategoryId { get; set; }
    public Guid? BrandId { get; set; }
    public Guid? CoffeeTypeId { get; set; }
    public Guid? BeanTypeId { get; set; }
    public Guid? RoastLevelId { get; set; }
    public Guid? OriginId { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsFeatured { get; set; }
    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();
    public Category Category { get; set; } = null!;
    public Brand? Brand { get; set; }
    public CoffeeType? CoffeeType { get; set; }
    public BeanType? BeanType { get; set; }
    public RoastLevel? RoastLevel { get; set; }
    public Origin? Origin { get; set; }
    public List<ProductImage> Images { get; set; } = [];
    public List<ProductVariant> Variants { get; set; } = [];

    public decimal CurrentPrice => DiscountedPrice ?? BasePrice;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Slug) || string.IsNullOrWhiteSpace(Sku))
        {
            throw new CommerceRuleException("Product name, slug and SKU are required.");
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(Slug, "^[a-z0-9]+(?:-[a-z0-9]+)*$", System.Text.RegularExpressions.RegexOptions.CultureInvariant))
        {
            throw new CommerceRuleException("Product slug must contain lowercase ASCII letters, numbers and single hyphens.");
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(Sku, "^[A-Z0-9]+(?:-[A-Z0-9]+)*$", System.Text.RegularExpressions.RegexOptions.CultureInvariant))
        {
            throw new CommerceRuleException("Product SKU must contain uppercase ASCII letters, numbers and single hyphens.");
        }

        if (BasePrice < 0 || DiscountedPrice < 0)
        {
            throw new CommerceRuleException("Product prices cannot be negative.");
        }

        if (DiscountedPrice > BasePrice)
        {
            throw new CommerceRuleException("Discounted price cannot exceed the base price.");
        }

        if (StockQuantity < 0)
        {
            throw new CommerceRuleException("Product stock cannot be negative.");
        }

        if (CriticalStockLevel < 0 || TaxRate is < 0 or > 100)
        {
            throw new CommerceRuleException("Product stock threshold or tax rate is invalid.");
        }
    }

    public void AdjustStock(int delta)
    {
        if (StockQuantity + delta < 0)
        {
            throw new CommerceRuleException("Insufficient product stock.");
        }

        StockQuantity += delta;
        ConcurrencyToken = Guid.NewGuid();
    }
}

public sealed class ProductImage : CommerceEntity
{
    public Guid ProductId { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public string AltText { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsPrimary { get; set; }
    public Product Product { get; set; } = null!;
}

public sealed class ProductVariant : SoftDeletableCommerceEntity, IConcurrencyTracked
{
    public Guid ProductId { get; set; }
    public decimal Weight { get; set; }
    public WeightUnit Unit { get; set; }
    public string Sku { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? DiscountedPrice { get; set; }
    public int StockQuantity { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();
    public Product Product { get; set; } = null!;

    public decimal CurrentPrice => DiscountedPrice ?? Price;

    public void Validate()
    {
        if (Weight <= 0 || Price < 0 || DiscountedPrice < 0 || DiscountedPrice > Price || StockQuantity < 0)
        {
            throw new CommerceRuleException("Product variant values are invalid.");
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(Sku, "^[A-Z0-9]+(?:-[A-Z0-9]+)*$", System.Text.RegularExpressions.RegexOptions.CultureInvariant))
        {
            throw new CommerceRuleException("Variant SKU must contain uppercase ASCII letters, numbers and single hyphens.");
        }
    }

    public void AdjustStock(int delta)
    {
        if (StockQuantity + delta < 0)
        {
            throw new CommerceRuleException("Insufficient variant stock.");
        }

        StockQuantity += delta;
        ConcurrencyToken = Guid.NewGuid();
    }
}
