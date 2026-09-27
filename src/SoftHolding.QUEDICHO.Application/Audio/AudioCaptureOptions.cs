namespace SoftHolding.QUEDICHO.Application.Audio;

public sealed record AudioCaptureOptions(string DeviceId, int TargetSampleRate = 16_000, int FrameDurationMilliseconds = 40);
