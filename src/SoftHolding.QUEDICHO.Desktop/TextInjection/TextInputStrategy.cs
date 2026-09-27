namespace SoftHolding.QUEDICHO.Desktop.TextInjection;

internal enum TextInjectionAttemptStatus
{
    Succeeded,
    RetryableFailure,
    Failed,
    TargetChanged
}

internal sealed record TextInjectionAttempt(TextInjectionAttemptStatus Status, string Reason)
{
    public static TextInjectionAttempt Success() => new(TextInjectionAttemptStatus.Succeeded, string.Empty);
}

internal interface ITextInputStrategy
{
    ValueTask<TextInjectionAttempt> TryInjectAsync(
        TextInjectionTarget target,
        string text,
        CancellationToken cancellationToken);
}
