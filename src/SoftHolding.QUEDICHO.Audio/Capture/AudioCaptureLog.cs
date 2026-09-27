using Microsoft.Extensions.Logging;

namespace SoftHolding.QUEDICHO.Audio.Capture;

internal static partial class AudioCaptureLog
{
    [LoggerMessage(2001, LogLevel.Warning, "Audio capture buffer overflow; an audio packet was dropped")]
    public static partial void BufferOverflow(ILogger logger);

    [LoggerMessage(2002, LogLevel.Information, "Starting WASAPI loopback capture on {DeviceName} with mix format {WaveFormat}")]
    public static partial void CaptureStarted(ILogger logger, string deviceName, string waveFormat);

    [LoggerMessage(2003, LogLevel.Information, "Audio endpoint changed; restarting loopback capture")]
    public static partial void EndpointRestart(ILogger logger);
}
