namespace SoftHolding.QUEDICHO.Desktop.TextInjection;

internal sealed record TextInjectionTarget(
    nint WindowHandle,
    int ProcessId,
    string ProcessName,
    string Identity);

internal sealed record TargetInspection(TextInjectionTarget? Target, string? SkipReason)
{
    public static TargetInspection Allow(TextInjectionTarget target) => new(target, null);
    public static TargetInspection Skip(string reason) => new(null, reason);
}
