using Microsoft.Extensions.Logging;

namespace SoftHolding.QUEDICHO.Desktop.TextInjection;

internal static partial class TextInjectionLog
{
    [LoggerMessage(4001, LogLevel.Information, "Text injection enabled")]
    public static partial void Enabled(ILogger logger);

    [LoggerMessage(4002, LogLevel.Information, "Text injection disabled")]
    public static partial void Disabled(ILogger logger);

    [LoggerMessage(4003, LogLevel.Information, "Text injection succeeded for target process {ProcessName}")]
    public static partial void Succeeded(ILogger logger, string processName);

    [LoggerMessage(4004, LogLevel.Warning, "Text injection failed for target process {ProcessName}: {Reason}")]
    public static partial void Failed(ILogger logger, string processName, string reason);

    [LoggerMessage(4005, LogLevel.Debug, "Text injection skipped: {Reason}")]
    public static partial void Skipped(ILogger logger, string reason);

    [LoggerMessage(4006, LogLevel.Warning, "Text injection queue is full; segment {SegmentId} was not queued")]
    public static partial void QueueFull(ILogger logger, Guid segmentId);

    [LoggerMessage(4007, LogLevel.Error, "Unexpected text injection worker failure")]
    public static partial void WorkerFailed(ILogger logger, Exception exception);

    [LoggerMessage(4008, LogLevel.Information, "Global hotkey {Gesture} registered")]
    public static partial void HotkeyRegistered(ILogger logger, string gesture);

    [LoggerMessage(4009, LogLevel.Warning, "Global hotkey {Gesture} could not be registered; Win32 error {ErrorCode}")]
    public static partial void HotkeyRegistrationFailed(ILogger logger, string gesture, int errorCode);

    [LoggerMessage(4010, LogLevel.Information, "Global hotkey {Gesture} unregistered")]
    public static partial void HotkeyUnregistered(ILogger logger, string gesture);

    [LoggerMessage(4011, LogLevel.Warning, "Text injection preferences could not be loaded; defaults will be used")]
    public static partial void PreferencesLoadFailed(ILogger logger, Exception exception);

    [LoggerMessage(4012, LogLevel.Warning, "Text injection preferences could not be saved")]
    public static partial void PreferencesSaveFailed(ILogger logger, Exception exception);

    [LoggerMessage(4013, LogLevel.Debug, "Clipboard restore skipped because clipboard ownership changed")]
    public static partial void ClipboardRestoreSkipped(ILogger logger);
}
