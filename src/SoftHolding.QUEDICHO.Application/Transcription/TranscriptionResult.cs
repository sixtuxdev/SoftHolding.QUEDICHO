namespace SoftHolding.QUEDICHO.Application.Transcription;

public sealed record TranscriptionResult(string Text, TimeSpan Start, TimeSpan End, double? Confidence);
