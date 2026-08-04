using AETKAHVE.Infrastructure.Identity;
using AETKAHVE.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AETKAHVE.Infrastructure.Persistence.Configurations;

public sealed class ManagementSessionConfiguration : IEntityTypeConfiguration<ManagementSession>
{
    public void Configure(EntityTypeBuilder<ManagementSession> builder)
    {
        builder.ToTable("ManagementSessions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SecurityStamp).HasMaxLength(256).IsRequired();
        builder.Property(x => x.RevocationReason).HasMaxLength(200);
        builder.Property(x => x.ConcurrencyToken).IsConcurrencyToken();
        builder.HasIndex(x => new { x.UserId, x.Portal, x.RevokedAtUtc });
        builder.HasIndex(x => x.AbsoluteExpiresAtUtc);
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
