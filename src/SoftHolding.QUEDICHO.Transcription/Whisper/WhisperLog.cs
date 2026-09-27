using Microsoft.Extensions.Logging;

namespace SoftHolding.QUEDICHO.Transcription.Whisper;

internal static partial class WhisperLog
{
    [LoggerMessage(3001, LogLevel.Information, "Whisper model {ModelPath} loaded for language {Language} with {Threads} threads")]
    public static partial void ModelLoaded(ILogger logger, string modelPath, string language, int threads);

    [LoggerMessage(3002, LogLevel.Information, "Transcribed {AudioDurationMs} ms in {ElapsedMs} ms")]
    public static partial void ChunkTranscribed(ILogger logger, double audioDurationMs, double elapsedMs);

    [LoggerMessage(3003, LogLevel.Debug, "Discarded uncertain segment with probability {Probability} and no-speech probability {NoSpeechProbability}")]
    public static partial void SegmentDiscarded(ILogger logger, double? probability, float noSpeechProbability);
}
