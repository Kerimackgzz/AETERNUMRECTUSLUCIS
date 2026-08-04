using AETKAHVE.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AETKAHVE.Infrastructure.Persistence.Configurations;

internal abstract class CommerceEntityConfiguration<T> : IEntityTypeConfiguration<T>
    where T : CommerceEntity
{
    public void Configure(EntityTypeBuilder<T> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();
        ConfigureEntity(builder);
    }

    protected abstract void ConfigureEntity(EntityTypeBuilder<T> builder);
}

internal static class CommercePropertyConfiguration
{
    public static PropertyBuilder<decimal> Money(this PropertyBuilder<decimal> property) =>
        property.HasPrecision(18, 2);

    public static PropertyBuilder<decimal?> Money(this PropertyBuilder<decimal?> property) =>
        property.HasPrecision(18, 2);

    public static PropertyBuilder<decimal> Rate(this PropertyBuilder<decimal> property) =>
        property.HasPrecision(5, 2);
}
