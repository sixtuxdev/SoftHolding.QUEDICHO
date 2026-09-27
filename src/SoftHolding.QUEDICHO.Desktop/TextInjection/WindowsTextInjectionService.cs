using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoftHolding.QUEDICHO.Application.TextInjection;
using SoftHolding.QUEDICHO.Domain.Transcripts;

namespace SoftHolding.QUEDICHO.Desktop.TextInjection;

internal sealed class WindowsTextInjectionService : BackgroundService, ITextInjectionService
{
    private const int RememberedSegmentLimit = 4_096;

    private readonly TextInjectionPreferencesStore _preferencesStore;
    private readonly WindowsForegroundTargetInspector _targetInspector;
    private readonly CompositeTextInjector _textInjector;
    private readonly ILogger<WindowsTextInjectionService> _logger;
    private readonly Channel<PendingSegment> _queue;
    private readonly ConcurrentDictionary<string, string> _previousTextByTarget = new();
    private readonly SemaphoreSlim _stateGate = new(1, 1);
    private readonly Lock _seenSegmentsGate = new();
    private readonly HashSet<Guid> _seenSegments = [];
    private readonly Queue<Guid> _seenSegmentOrder = [];
    private long _activationVersion;
    private int _isEnabled;
    private string _globalHotkey;

    public WindowsTextInjectionService(
        IOptions<TextInjectionOptions> options,
        TextInjectionPreferencesStore preferencesStore,
        WindowsForegroundTargetInspector targetInspector,
        CompositeTextInjector textInjector,
        ILogger<WindowsTextInjectionService> logger)
    {
        _preferencesStore = preferencesStore;
        _targetInspector = targetInspector;
        _textInjector = textInjector;
        _logger = logger;
        _globalHotkey = string.IsNullOrWhiteSpace(options.Value.GlobalHotkey)
            ? "Ctrl+Shift+Y"
            : options.Value.GlobalHotkey.Trim();
        _queue = Channel.CreateBounded<PendingSegment>(new BoundedChannelOptions(
            Math.Clamp(options.Value.QueueCapacity, 16, 2_048))
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public bool IsEnabled => Volatile.Read(ref _isEnabled) == 1;
    public string GlobalHotkey => _globalHotkey;

    public event EventHandler<TextInjectionStateChangedEventArgs>? StateChanged;

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        var preferences = await _preferencesStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        _globalHotkey = preferences.GlobalHotkey;
        Volatile.Write(ref _isEnabled, preferences.Enabled ? 1 : 0);
        Interlocked.Increment(ref _activationVersion);
        if (preferences.Enabled)
        {
            TextInjectionLog.Enabled(_logger);
        }
        else
        {
            TextInjectionLog.Disabled(_logger);
        }

        await base.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        await _stateGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await SetEnabledCoreAsync(enabled, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _stateGate.Release();
        }
    }

    public async ValueTask ToggleAsync(CancellationToken cancellationToken = default)
    {
        await _stateGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await SetEnabledCoreAsync(!IsEnabled, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _stateGate.Release();
        }
    }

    private async ValueTask SetEnabledCoreAsync(bool enabled, CancellationToken cancellationToken)
    {
        var requestedValue = enabled ? 1 : 0;
        if (Interlocked.Exchange(ref _isEnabled, requestedValue) == requestedValue)
        {
            return;
        }

        Interlocked.Increment(ref _activationVersion);
        _previousTextByTarget.Clear();
        if (enabled)
        {
            TextInjectionLog.Enabled(_logger);
        }
        else
        {
            TextInjectionLog.Disabled(_logger);
        }

        await _preferencesStore.SaveAsync(new TextInjectionPreferences
        {
            Enabled = enabled,
            GlobalHotkey = _globalHotkey
        }, cancellationToken).ConfigureAwait(false);
        StateChanged?.Invoke(this, new TextInjectionStateChangedEventArgs(enabled));
    }

    public void Enqueue(TranscriptSegment segment)
    {
        ArgumentNullException.ThrowIfNull(segment);
        if (!IsEnabled || !segment.IsFinal || string.IsNullOrWhiteSpace(segment.Text))
        {
            return;
        }

        if (!RememberSegment(segment.Id))
        {
            TextInjectionLog.Skipped(_logger, "duplicate segment");
            return;
        }

        var pending = new PendingSegment(
            segment.Id,
            segment.Text,
            Volatile.Read(ref _activationVersion));
        if (!_queue.Writer.TryWrite(pending))
        {
            ForgetSegment(segment.Id);
            TextInjectionLog.QueueFull(_logger, segment.Id);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var pending in _queue.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
            {
                try
                {
                    await ProcessAsync(pending, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    TextInjectionLog.WorkerFailed(_logger, exception);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _queue.Writer.TryComplete();
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    public override void Dispose()
    {
        _stateGate.Dispose();
        base.Dispose();
    }

    private async Task ProcessAsync(PendingSegment pending, CancellationToken cancellationToken)
    {
        if (!IsEnabled || pending.ActivationVersion != Volatile.Read(ref _activationVersion))
        {
            TextInjectionLog.Skipped(_logger, "feature disabled or activation changed");
            return;
        }

        var inspection = _targetInspector.Inspect();
        if (inspection.Target is null)
        {
            TextInjectionLog.Skipped(_logger, inspection.SkipReason ?? "no valid target");
            return;
        }

        var target = inspection.Target;
        _previousTextByTarget.TryGetValue(target.Identity, out var previousText);
        var textToInsert = TextSegmentSpacingPolicy.PrepareForInsertion(previousText, pending.Text);
        var attempt = await _textInjector.InjectAsync(target, textToInsert, cancellationToken)
            .ConfigureAwait(false);
        if (attempt.Status == TextInjectionAttemptStatus.Succeeded)
        {
            _previousTextByTarget[target.Identity] = pending.Text;
            TextInjectionLog.Succeeded(_logger, target.ProcessName);
        }
        else
        {
            TextInjectionLog.Failed(_logger, target.ProcessName, attempt.Reason);
        }
    }

    private bool RememberSegment(Guid segmentId)
    {
        lock (_seenSegmentsGate)
        {
            if (!_seenSegments.Add(segmentId))
            {
                return false;
            }

            _seenSegmentOrder.Enqueue(segmentId);
            while (_seenSegmentOrder.Count > RememberedSegmentLimit)
            {
                _seenSegments.Remove(_seenSegmentOrder.Dequeue());
            }

            return true;
        }
    }

    private void ForgetSegment(Guid segmentId)
    {
        lock (_seenSegmentsGate)
        {
            _seenSegments.Remove(segmentId);
        }
    }

    private sealed record PendingSegment(Guid SegmentId, string Text, long ActivationVersion);
}
