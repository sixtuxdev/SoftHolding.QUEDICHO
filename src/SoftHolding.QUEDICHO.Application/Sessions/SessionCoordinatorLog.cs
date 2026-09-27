using Microsoft.Extensions.Logging;

namespace SoftHolding.QUEDICHO.Application.Sessions;

internal static partial class SessionCoordinatorLog
{
    [LoggerMessage(1001, LogLevel.Information, "Transcription session {SessionId} started with device {DeviceId} and provider {Provider}")]
    public static partial void SessionStarted(ILogger logger, Guid sessionId, string deviceId, string provider);

    [LoggerMessage(1002, LogLevel.Information, "Transcription session {SessionId} paused")]
    public static partial void SessionPaused(ILogger logger, Guid sessionId);

    [LoggerMessage(1003, LogLevel.Information, "Transcription session {SessionId} resumed")]
    public static partial void SessionResumed(ILogger logger, Guid sessionId);

    [LoggerMessage(1004, LogLevel.Information, "Transcription session {SessionId} completed")]
    public static partial void SessionCompleted(ILogger logger, Guid sessionId);

    [LoggerMessage(1005, LogLevel.Error, "Transcription pipeline failed for session {SessionId}")]
    public static partial void PipelineFailed(ILogger logger, Guid? sessionId, Exception exception);
}
