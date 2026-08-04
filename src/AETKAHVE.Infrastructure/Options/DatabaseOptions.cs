namespace AETKAHVE.Infrastructure.Options;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string Provider { get; set; } = "SqlServer";

    public string ConnectionString { get; set; } = string.Empty;
}

