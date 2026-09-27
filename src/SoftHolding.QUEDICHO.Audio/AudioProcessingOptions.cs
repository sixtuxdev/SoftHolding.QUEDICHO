namespace SoftHolding.QUEDICHO.Audio;

public sealed class AudioProcessingOptions
{
    public const string SectionName = "AudioProcessing";

    public double SpeechThresholdDb { get; set; } = -42;
    public int PreRollMilliseconds { get; set; } = 300;
    public int SilenceToCloseMilliseconds { get; set; } = 600;
    public int MinimumSpeechMilliseconds { get; set; } = 300;
    public int MaximumChunkMilliseconds { get; set; } = 5_000;
}
