namespace SoftHolding.QUEDICHO.Transcription.Whisper;

public sealed class WhisperOptions
{
    public const string SectionName = "Whisper";

    public string ModelType { get; set; } = "Base";
    public string ModelFileName { get; set; } = "ggml-base.bin";
    public string Language { get; set; } = "es";
    public int Threads { get; set; }
    public int BeamSize { get; set; } = 5;
    public float MinimumSegmentProbability { get; set; } = 0.25f;
    public float MaximumNoSpeechProbability { get; set; } = 0.6f;
}
