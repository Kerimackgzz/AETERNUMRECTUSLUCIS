using AETKAHVE.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AETKAHVE.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ActionType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.EntityType).HasMaxLength(100);
        builder.Property(x => x.EntityId).HasMaxLength(100);
        builder.Property(x => x.OldValues).HasMaxLength(4000);
        builder.Property(x => x.NewValues).HasMaxLength(4000);
        builder.Property(x => x.Description).HasMaxLength(500).IsRequired();
        builder.Property(x => x.IpAddress).HasMaxLength(64);
        builder.Property(x => x.UserAgent).HasMaxLength(512);
        builder.Property(x => x.Route).HasMaxLength(256);
        builder.Property(x => x.CorrelationId).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => x.CreatedAtUtc);
        builder.HasIndex(x => new { x.AdminUserId, x.ActionType });
    }
}

