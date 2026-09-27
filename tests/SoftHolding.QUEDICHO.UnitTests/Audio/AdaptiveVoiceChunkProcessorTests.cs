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
            StreamingChunkMilliseconds = 5_000,
            OverlapMilliseconds = 200
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

    [Fact]
    public void ProcessorEmitsOverlappingChunksDuringContinuousSpeech()
    {
        var processor = new AdaptiveVoiceChunkProcessor(Options.Create(new AudioProcessingOptions
        {
            SpeechThresholdDb = -40,
            PreRollMilliseconds = 160,
            SilenceToCloseMilliseconds = 240,
            MinimumSpeechMilliseconds = 160,
            StreamingChunkMilliseconds = 1_200,
            OverlapMilliseconds = 240
        }));

        var emitted = new List<AudioChunk>();
        var position = TimeSpan.Zero;
        for (var index = 0; index < 54; index++)
        {
            var frame = new AudioFrame(
                Enumerable.Repeat(0.1f, 640).ToArray(),
                16_000,
                position);
            emitted.AddRange(processor.Process(frame));
            position += frame.Duration;
        }

        Assert.Equal(2, emitted.Count);
        Assert.Equal(TimeSpan.FromMilliseconds(1_200), emitted[0].Duration);
        Assert.Equal(TimeSpan.FromMilliseconds(960), emitted[1].Start);
        Assert.Equal(TimeSpan.FromMilliseconds(1_200), emitted[1].Duration);
    }

    [Fact]
    public void ProcessorEmitsShortWordEndingAfterAStreamingChunk()
    {
        var processor = new AdaptiveVoiceChunkProcessor(Options.Create(new AudioProcessingOptions
        {
            SpeechThresholdDb = -40,
            PreRollMilliseconds = 160,
            SilenceToCloseMilliseconds = 240,
            MinimumSpeechMilliseconds = 160,
            StreamingChunkMilliseconds = 1_200,
            OverlapMilliseconds = 240
        }));

        var emitted = new List<AudioChunk>();
        var position = TimeSpan.Zero;
        foreach (var amplitude in Enumerable.Repeat(0.1f, 32).Concat(Enumerable.Repeat(0f, 6)))
        {
            var frame = new AudioFrame(
                Enumerable.Repeat(amplitude, 640).ToArray(),
                16_000,
                position);
            emitted.AddRange(processor.Process(frame));
            position += frame.Duration;
        }

        Assert.Equal(2, emitted.Count);
        Assert.Equal(TimeSpan.FromMilliseconds(960), emitted[1].Start);
        Assert.Equal(TimeSpan.FromMilliseconds(560), emitted[1].Duration);
    }
}
