using SoftHolding.QUEDICHO.Domain.Transcripts;

namespace SoftHolding.QUEDICHO.Application.TextInjection;

public interface ITextInjectionService
{
    bool IsEnabled { get; }
    string GlobalHotkey { get; }

    event EventHandler<TextInjectionStateChangedEventArgs>? StateChanged;

    ValueTask SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default);
    ValueTask ToggleAsync(CancellationToken cancellationToken = default);
    void Enqueue(TranscriptSegment segment);
}
