namespace SoftHolding.QUEDICHO.Application.Audio;

public interface IAudioCaptureSource
{
    IAsyncEnumerable<AudioFrame> CaptureAsync(
        AudioCaptureOptions options,
        CancellationToken cancellationToken = default);
}
