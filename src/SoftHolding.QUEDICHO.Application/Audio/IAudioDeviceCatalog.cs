namespace SoftHolding.QUEDICHO.Application.Audio;

public interface IAudioDeviceCatalog
{
    event EventHandler? DevicesChanged;

    Task<IReadOnlyList<AudioDeviceInfo>> GetRenderDevicesAsync(CancellationToken cancellationToken = default);

    Task<AudioDeviceInfo> ResolveAsync(string? deviceId, CancellationToken cancellationToken = default);
}
