namespace SoftHolding.QUEDICHO.Application.Audio;

public interface IAudioChunkProcessor
{
    void Reset();

    IReadOnlyList<AudioChunk> Process(AudioFrame frame);

    AudioChunk? Flush();
}
