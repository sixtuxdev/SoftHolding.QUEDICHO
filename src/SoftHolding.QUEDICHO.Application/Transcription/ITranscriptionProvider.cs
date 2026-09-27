using SoftHolding.QUEDICHO.Application.Audio;

namespace SoftHolding.QUEDICHO.Application.Transcription;

public interface ITranscriptionProvider
{
    string Name { get; }

    event EventHandler<string>? StatusChanged;

    Task InitializeAsync(CancellationToken cancellationToken = default);

    IAsyncEnumerable<TranscriptionResult> TranscribeAsync(
        AudioChunk chunk,
        CancellationToken cancellationToken = default);
}
