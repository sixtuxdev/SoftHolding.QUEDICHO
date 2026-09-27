namespace SoftHolding.QUEDICHO.Desktop.TextInjection;

internal sealed class SendInputUnicodeStrategy : ITextInputStrategy
{
    public ValueTask<TextInjectionAttempt> TryInjectAsync(
        TextInjectionTarget target,
        string text,
        CancellationToken cancellationToken) =>
        WindowsKeyboardInput.SendUnicodeTextAsync(target, text, cancellationToken);
}
