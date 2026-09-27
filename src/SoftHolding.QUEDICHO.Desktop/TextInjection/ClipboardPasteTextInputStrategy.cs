using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SoftHolding.QUEDICHO.Desktop.TextInjection;

internal sealed class ClipboardPasteTextInputStrategy(
    StaClipboardService clipboardService,
    IOptions<TextInjectionOptions> options,
    ILogger<ClipboardPasteTextInputStrategy> logger) : ITextInputStrategy
{
    private readonly int _restoreDelayMilliseconds = Math.Clamp(
        options.Value.ClipboardRestoreDelayMilliseconds,
        50,
        1_000);

    public async ValueTask<TextInjectionAttempt> TryInjectAsync(
        TextInjectionTarget target,
        string text,
        CancellationToken cancellationToken)
    {
        var lease = await clipboardService.TrySetTextAsync(text).ConfigureAwait(false);
        if (lease is null)
        {
            return new TextInjectionAttempt(
                TextInjectionAttemptStatus.RetryableFailure,
                "clipboard is unavailable");
        }

        TextInjectionAttempt attempt;
        try
        {
            attempt = await WindowsKeyboardInput.SendPasteShortcutAsync(target, cancellationToken)
                .ConfigureAwait(false);
            if (attempt.Status == TextInjectionAttemptStatus.Succeeded)
            {
                await Task.Delay(_restoreDelayMilliseconds, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            var restored = await clipboardService.RestoreIfUnchangedAsync(lease).ConfigureAwait(false);
            if (!restored)
            {
                TextInjectionLog.ClipboardRestoreSkipped(logger);
            }
        }

        return attempt;
    }
}
