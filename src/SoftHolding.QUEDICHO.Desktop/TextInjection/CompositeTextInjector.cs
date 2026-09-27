namespace SoftHolding.QUEDICHO.Desktop.TextInjection;

internal sealed class CompositeTextInjector(
    ClipboardPasteTextInputStrategy clipboardStrategy,
    SendInputUnicodeStrategy unicodeStrategy)
{
    public async ValueTask<TextInjectionAttempt> InjectAsync(
        TextInjectionTarget target,
        string text,
        CancellationToken cancellationToken)
    {
        var primaryAttempt = await clipboardStrategy.TryInjectAsync(target, text, cancellationToken)
            .ConfigureAwait(false);
        if (primaryAttempt.Status != TextInjectionAttemptStatus.RetryableFailure)
        {
            return primaryAttempt;
        }

        return await unicodeStrategy.TryInjectAsync(target, text, cancellationToken)
            .ConfigureAwait(false);
    }
}
