using NAudio.CoreAudioApi;
using SoftHolding.QUEDICHO.Application.Audio;

namespace SoftHolding.QUEDICHO.Audio.Devices;

public sealed class WasapiAudioDeviceCatalog : IAudioDeviceCatalog, IDisposable
{
    private readonly MMDeviceEnumerator _enumerator = new();
    private readonly MMDeviceNotificationClient _notificationClient;
    private bool _disposed;

    public WasapiAudioDeviceCatalog()
    {
        _notificationClient = _enumerator.CreateNotificationClient(useSynchronizationContext: false);
        _notificationClient.DeviceStateChanged += (_, _) => RaiseDevicesChanged();
        _notificationClient.DeviceAdded += (_, _) => RaiseDevicesChanged();
        _notificationClient.DeviceRemoved += (_, _) => RaiseDevicesChanged();
        _notificationClient.DefaultDeviceChanged += (_, eventArgs) =>
        {
            if (eventArgs.Flow == DataFlow.Render && eventArgs.Role is Role.Multimedia or Role.Console)
            {
                RaiseDevicesChanged();
            }
        };
    }

    public event EventHandler? DevicesChanged;

    public Task<IReadOnlyList<AudioDeviceInfo>> GetRenderDevicesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var defaultId = TryGetDefaultDevice()?.ID;
        var devices = _enumerator
            .EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
            .Select(device => new AudioDeviceInfo(device.ID, device.FriendlyName, device.ID == defaultId))
            .OrderByDescending(device => device.IsDefault)
            .ThenBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        return Task.FromResult<IReadOnlyList<AudioDeviceInfo>>(devices);
    }

    public async Task<AudioDeviceInfo> ResolveAsync(string? deviceId, CancellationToken cancellationToken = default)
    {
        var devices = await GetRenderDevicesAsync(cancellationToken).ConfigureAwait(false);
        var selected = string.IsNullOrWhiteSpace(deviceId)
            ? devices.FirstOrDefault(device => device.IsDefault)
            : devices.FirstOrDefault(device => string.Equals(device.Id, deviceId, StringComparison.Ordinal));

        selected ??= devices.FirstOrDefault(device => device.IsDefault) ?? (devices.Count > 0 ? devices[0] : null);
        return selected ?? throw new InvalidOperationException("No active audio output device is available.");
    }

    internal MMDevice GetDevice(string? deviceId)
    {
        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            try
            {
                return _enumerator.GetDevice(deviceId);
            }
            catch
            {
                // The endpoint may have disappeared between enumeration and capture.
            }
        }

        return TryGetDefaultDevice()
            ?? throw new InvalidOperationException("No default audio output device is available.");
    }

    private MMDevice? TryGetDefaultDevice()
    {
        try
        {
            return _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        }
        catch
        {
            return null;
        }
    }

    private void RaiseDevicesChanged() => DevicesChanged?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _notificationClient.Dispose();
        _enumerator.Dispose();
        _disposed = true;
    }
}
