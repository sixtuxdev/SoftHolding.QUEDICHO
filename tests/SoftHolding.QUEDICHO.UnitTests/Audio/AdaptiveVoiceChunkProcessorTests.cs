using Microsoft.Extensions.Options;
using SoftHolding.QUEDICHO.Application.Audio;
using SoftHolding.QUEDICHO.Audio;
using SoftHolding.QUEDICHO.Audio.Processing;

namespace SoftHolding.QUEDICHO.UnitTests.Audio;

public sealed class AdaptiveVoiceChunkProcessorTests
{
    [Fact]
    public void ProcessorEmitsChunkAfterSpeechAndTrailingSilence()
    {
        var processor = new AdaptiveVoiceChunkProcessor(Options.Create(new AudioProcessingOptions
        {
            SpeechThresholdDb = -40,
            PreRollMilliseconds = 100,
            SilenceToCloseMilliseconds = 200,
            MinimumSpeechMilliseconds = 200,
            MaximumChunkMilliseconds = 5_000
        }));

        var emitted = new List<AudioChunk>();
        var position = TimeSpan.Zero;
        foreach (var amplitude in new[] { 0f, 0.1f, 0.1f, 0.1f, 0f, 0f })
        {
            var frame = new AudioFrame(
                Enumerable.Repeat(amplitude, 1_600).ToArray(),
                16_000,
                position);
            emitted.AddRange(processor.Process(frame));
            position += frame.Duration;
        }

        var chunk = Assert.Single(emitted);
        Assert.True(chunk.Duration >= TimeSpan.FromMilliseconds(400));
        Assert.Equal(TimeSpan.FromMilliseconds(100), chunk.Start);
    }
}
