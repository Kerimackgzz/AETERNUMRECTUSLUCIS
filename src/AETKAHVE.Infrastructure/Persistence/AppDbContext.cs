using AETKAHVE.Infrastructure.Identity;
using AETKAHVE.Infrastructure.Security;
using AETKAHVE.Domain.Commerce;
using AETKAHVE.Domain.Common;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AETKAHVE.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    public DbSet<ManagementSession> ManagementSessions => Set<ManagementSession>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<CoffeeType> CoffeeTypes => Set<CoffeeType>();
    public DbSet<BeanType> BeanTypes => Set<BeanType>();
    public DbSet<RoastLevel> RoastLevels => Set<RoastLevel>();
    public DbSet<Origin> Origins => Set<Origin>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<CampaignProduct> CampaignProducts => Set<CampaignProduct>();
    public DbSet<CampaignCategory> CampaignCategories => Set<CampaignCategory>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<CouponUsage> CouponUsages => Set<CouponUsage>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderStatusHistory> OrderStatusHistory => Set<OrderStatusHistory>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<ShipmentStatusHistory> ShipmentStatusHistory => Set<ShipmentStatusHistory>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<ReturnRequest> ReturnRequests => Set<ReturnRequest>();
    public DbSet<ReturnItem> ReturnItems => Set<ReturnItem>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        RotateConcurrencyTokens();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        RotateConcurrencyTokens();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void RotateConcurrencyTokens()
    {
        foreach (var entry in ChangeTracker.Entries<IConcurrencyTracked>().Where(x => x.State == EntityState.Modified))
            entry.Entity.ConcurrencyToken = Guid.NewGuid();
    }
}

