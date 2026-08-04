using System.ComponentModel.DataAnnotations.Schema;

namespace AETKAHVE.Domain.Common;

public interface IConcurrencyTracked
{
    Guid ConcurrencyToken { get; set; }
}

[NotMapped]
public abstract class CommerceEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

[NotMapped]
public abstract class SoftDeletableCommerceEntity : CommerceEntity
{
    public DateTimeOffset? DeletedAtUtc { get; set; }
}

public sealed class CommerceRuleException(string message) : InvalidOperationException(message);
