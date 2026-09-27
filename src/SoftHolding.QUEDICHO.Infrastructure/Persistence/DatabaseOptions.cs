namespace SoftHolding.QUEDICHO.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string FileName { get; set; } = "quedicho.db";
}
