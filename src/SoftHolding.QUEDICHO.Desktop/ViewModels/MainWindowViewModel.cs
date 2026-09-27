using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoftHolding.QUEDICHO.Application.Audio;
using SoftHolding.QUEDICHO.Application.Sessions;
using SoftHolding.QUEDICHO.Application.Transcription;
using WpfApplication = System.Windows.Application;

namespace SoftHolding.QUEDICHO.Desktop.ViewModels;

public partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly IAudioDeviceCatalog _deviceCatalog;
    private readonly ITranscriptionSessionCoordinator _coordinator;
    private readonly ITranscriptionProvider _transcriptionProvider;
    private readonly Stopwatch _elapsed = new();
    private readonly DispatcherTimer _timer;
    private bool _disposed;

    [ObservableProperty]
    private string _statusText = "Listo";

    [ObservableProperty]
    private string _elapsedText = "00:00:00";

    [ObservableProperty]
    private AudioDeviceItemViewModel? _selectedDevice;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTranscriptEmpty))]
    private int _segmentCount;

    [ObservableProperty]
    private bool _isBusy;

    public MainWindowViewModel(
        IAudioDeviceCatalog deviceCatalog,
        ITranscriptionSessionCoordinator coordinator,
        ITranscriptionProvider transcriptionProvider)
    {
        _deviceCatalog = deviceCatalog;
        _coordinator = coordinator;
        _transcriptionProvider = transcriptionProvider;
        _timer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, OnTimerTick, WpfApplication.Current.Dispatcher);
        _timer.Stop();

        _coordinator.StateChanged += OnStateChanged;
        _coordinator.SegmentCommitted += OnSegmentCommitted;
        _coordinator.ErrorOccurred += OnErrorOccurred;
        _transcriptionProvider.StatusChanged += OnProviderStatusChanged;
        _deviceCatalog.DevicesChanged += OnDevicesChanged;
    }

    public ObservableCollection<AudioDeviceItemViewModel> Devices { get; } = [];
    public ObservableCollection<TranscriptLineViewModel> Segments { get; } = [];

    public bool IsTranscriptEmpty => SegmentCount == 0;
    public bool CanStart => !IsBusy && _coordinator.State is TranscriptionSessionState.Idle or TranscriptionSessionState.Completed or TranscriptionSessionState.Faulted && SelectedDevice is not null;
    public bool CanPauseOrResume => !IsBusy && _coordinator.State is TranscriptionSessionState.Listening or TranscriptionSessionState.Paused;
    public bool CanStop => !IsBusy && _coordinator.State is TranscriptionSessionState.Listening or TranscriptionSessionState.Paused;
    public bool IsDeviceSelectionEnabled => !IsBusy && _coordinator.State is not TranscriptionSessionState.Listening and not TranscriptionSessionState.Paused;
    public string PauseButtonText => _coordinator.State == TranscriptionSessionState.Paused ? "REANUDAR" : "PAUSAR";

    public async Task InitializeAsync() => await ReloadDevicesAsync();

    [RelayCommand]
    private async Task StartAsync()
    {
        if (SelectedDevice is null)
        {
            return;
        }

        IsBusy = true;
        NotifyCommandState();
        try
        {
            Segments.Clear();
            SegmentCount = 0;
            _elapsed.Reset();
            ElapsedText = "00:00:00";
            await _coordinator.StartAsync(new StartSessionRequest(SelectedDevice.Id));
            _elapsed.Start();
            _timer.Start();
        }
        catch (Exception exception)
        {
            StatusText = "Error";
            MessageBox.Show(exception.Message, "No se pudo iniciar", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            NotifyCommandState();
        }
    }

    [RelayCommand]
    private async Task PauseResumeAsync()
    {
        IsBusy = true;
        NotifyCommandState();
        try
        {
            if (_coordinator.State == TranscriptionSessionState.Paused)
            {
                await _coordinator.ResumeAsync();
                _elapsed.Start();
            }
            else
            {
                await _coordinator.PauseAsync();
                _elapsed.Stop();
            }
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "No se pudo cambiar el estado", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            NotifyCommandState();
        }
    }

    [RelayCommand]
    private async Task StopAsync()
    {
        IsBusy = true;
        NotifyCommandState();
        try
        {
            await _coordinator.StopAsync();
            _elapsed.Stop();
            _timer.Stop();
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "No se pudo detener", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            NotifyCommandState();
        }
    }

    private async Task ReloadDevicesAsync()
    {
        var selectedId = SelectedDevice?.Id;
        var models = await _deviceCatalog.GetRenderDevicesAsync();
        await WpfApplication.Current.Dispatcher.InvokeAsync(() =>
        {
            Devices.Clear();
            foreach (var model in models)
            {
                Devices.Add(AudioDeviceItemViewModel.FromModel(model));
            }

            SelectedDevice = Devices.FirstOrDefault(device => device.Id == selectedId)
                             ?? Devices.FirstOrDefault(device => device.IsDefault)
                             ?? Devices.FirstOrDefault();
            NotifyCommandState();
        });
    }

    private void OnDevicesChanged(object? sender, EventArgs eventArgs) =>
        _ = WpfApplication.Current.Dispatcher.InvokeAsync(async () => await ReloadDevicesAsync());

    private void OnStateChanged(object? sender, SessionStateChangedEventArgs eventArgs) =>
        _ = WpfApplication.Current.Dispatcher.InvokeAsync(() =>
        {
            StatusText = eventArgs.Message;
            OnPropertyChanged(nameof(PauseButtonText));
            NotifyCommandState();
        });

    private void OnSegmentCommitted(object? sender, TranscriptSegmentEventArgs eventArgs) =>
        _ = WpfApplication.Current.Dispatcher.InvokeAsync(() =>
        {
            Segments.Add(new TranscriptLineViewModel(
                eventArgs.Segment.StartTime.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture),
                eventArgs.Segment.Text));
            SegmentCount = Segments.Count;
        });

    private void OnErrorOccurred(object? sender, string error) =>
        _ = WpfApplication.Current.Dispatcher.InvokeAsync(() =>
        {
            _elapsed.Stop();
            _timer.Stop();
            MessageBox.Show(error, "La sesión se interrumpió", MessageBoxButton.OK, MessageBoxImage.Warning);
        });

    private void OnProviderStatusChanged(object? sender, string status) =>
        _ = WpfApplication.Current.Dispatcher.InvokeAsync(() => StatusText = status);

    private void OnTimerTick(object? sender, EventArgs eventArgs) =>
        ElapsedText = _elapsed.Elapsed.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);

    private void NotifyCommandState()
    {
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanPauseOrResume));
        OnPropertyChanged(nameof(CanStop));
        OnPropertyChanged(nameof(IsDeviceSelectionEnabled));
        OnPropertyChanged(nameof(PauseButtonText));
    }

    partial void OnIsBusyChanged(bool value) => NotifyCommandState();
    partial void OnSelectedDeviceChanged(AudioDeviceItemViewModel? value) => NotifyCommandState();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _timer.Stop();
        _coordinator.StateChanged -= OnStateChanged;
        _coordinator.SegmentCommitted -= OnSegmentCommitted;
        _coordinator.ErrorOccurred -= OnErrorOccurred;
        _transcriptionProvider.StatusChanged -= OnProviderStatusChanged;
        _deviceCatalog.DevicesChanged -= OnDevicesChanged;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
