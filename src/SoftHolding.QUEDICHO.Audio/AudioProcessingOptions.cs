namespace SoftHolding.QUEDICHO.Audio;

public sealed class AudioProcessingOptions
{
    public const string SectionName = "AudioProcessing";

    public double SpeechThresholdDb { get; set; } = -42;
    public int PreRollMilliseconds { get; set; } = 250;
    public int SilenceToCloseMilliseconds { get; set; } = 400;
    public int MinimumSpeechMilliseconds { get; set; } = 300;
    public int StreamingChunkMilliseconds { get; set; } = 5_000;
    public int OverlapMilliseconds { get; set; } = 1_000;
}
