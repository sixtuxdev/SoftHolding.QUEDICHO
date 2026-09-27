using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using SoftHolding.QUEDICHO.Application.Audio;
using SoftHolding.QUEDICHO.Application.Persistence;
using SoftHolding.QUEDICHO.Application.Transcription;
using SoftHolding.QUEDICHO.Domain.Sessions;
using SoftHolding.QUEDICHO.Domain.Transcripts;

namespace SoftHolding.QUEDICHO.Application.Sessions;

public sealed class TranscriptionSessionCoordinator(
    IAudioDeviceCatalog audioDeviceCatalog,
    IAudioCaptureSource audioCaptureSource,
    IAudioChunkProcessor audioChunkProcessor,
    ITranscriptionProvider transcriptionProvider,
    ISessionRepository sessionRepository,
    TimeProvider timeProvider,
    ILogger<TranscriptionSessionCoordinator> logger) : ITranscriptionSessionCoordinator, IAsyncDisposable, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource? _pipelineCancellation;
    private Task? _pipelineTask;
    private TranscriptionSession? _session;
    private StartSessionRequest? _request;
    private TimeSpan _captureOffset;
    private TimeSpan _lastCommittedEnd;
    private string? _lastCommittedText;

    public event EventHandler<SessionStateChangedEventArgs>? StateChanged;
    public event EventHandler<TranscriptSegmentEventArgs>? SegmentCommitted;
    public event EventHandler<string>? ErrorOccurred;

    public TranscriptionSessionState State { get; private set; } = TranscriptionSessionState.Idle;
    public Guid? CurrentSessionId => _session?.Id;

    public async Task StartAsync(StartSessionRequest request, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (State is TranscriptionSessionState.Preparing or TranscriptionSessionState.Listening or TranscriptionSessionState.Paused or TranscriptionSessionState.Stopping)
            {
                throw new InvalidOperationException("A transcription session is already active.");
            }

            SetState(TranscriptionSessionState.Preparing, "Preparando el modelo local...");
            await transcriptionProvider.InitializeAsync(cancellationToken).ConfigureAwait(false);

            var device = await audioDeviceCatalog.ResolveAsync(request.DeviceId, cancellationToken).ConfigureAwait(false);
            var now = timeProvider.GetUtcNow();
            var title = string.IsNullOrWhiteSpace(request.Title)
                ? $"Sesión {now.ToLocalTime():yyyy-MM-dd HH:mm}"
                : request.Title.Trim();

            _session = TranscriptionSession.Create(
                title,
                request.Language,
                transcriptionProvider.Name,
                device.Id,
                device.Name,
                now);
            _request = request with { DeviceId = device.Id };
            _captureOffset = TimeSpan.Zero;
            _lastCommittedEnd = TimeSpan.Zero;
            _lastCommittedText = null;

            await sessionRepository.CreateAsync(_session, cancellationToken).ConfigureAwait(false);
            _session.MarkListening();
            await sessionRepository.UpdateAsync(_session, cancellationToken).ConfigureAwait(false);

            StartPipeline();
            SetState(TranscriptionSessionState.Listening, $"Escuchando: {device.Name}");
            SessionCoordinatorLog.SessionStarted(logger, _session.Id, device.Id, transcriptionProvider.Name);
        }
        catch
        {
            if (_session is not null && _session.Status is not SessionStatus.Listening)
            {
                _session.Fail("The session could not be started.", timeProvider.GetUtcNow());
                await sessionRepository.UpdateAsync(_session, CancellationToken.None).ConfigureAwait(false);
            }

            SetState(TranscriptionSessionState.Faulted, "No se pudo iniciar la sesión.");
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task PauseAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureSessionState(TranscriptionSessionState.Listening);
            await StopPipelineAsync().ConfigureAwait(false);
            _session!.Pause();
            await sessionRepository.UpdateAsync(_session, cancellationToken).ConfigureAwait(false);
            SetState(TranscriptionSessionState.Paused, "Pausado");
            SessionCoordinatorLog.SessionPaused(logger, _session.Id);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ResumeAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureSessionState(TranscriptionSessionState.Paused);
            _session!.MarkListening();
            await sessionRepository.UpdateAsync(_session, cancellationToken).ConfigureAwait(false);
            StartPipeline();
            SetState(TranscriptionSessionState.Listening, "Escuchando");
            SessionCoordinatorLog.SessionResumed(logger, _session.Id);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_session is null || State is TranscriptionSessionState.Idle or TranscriptionSessionState.Completed)
            {
                return;
            }

            if (_session.Status == SessionStatus.Faulted)
            {
                SetState(TranscriptionSessionState.Faulted, "La sesión terminó con error");
                return;
            }

            SetState(TranscriptionSessionState.Stopping, "Finalizando la transcripción...");
            if (_session.Status is SessionStatus.Listening or SessionStatus.Paused or SessionStatus.Starting)
            {
                _session.BeginStopping();
                await sessionRepository.UpdateAsync(_session, cancellationToken).ConfigureAwait(false);
            }

            await StopPipelineAsync().ConfigureAwait(false);

            if (_session.Status != SessionStatus.Faulted)
            {
                _session.Complete(timeProvider.GetUtcNow());
                await sessionRepository.UpdateAsync(_session, cancellationToken).ConfigureAwait(false);
                SetState(TranscriptionSessionState.Completed, "Sesión guardada");
                SessionCoordinatorLog.SessionCompleted(logger, _session.Id);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private void StartPipeline()
    {
        _pipelineCancellation = new CancellationTokenSource();
        _pipelineTask = RunPipelineGuardedAsync(_pipelineCancellation.Token);
    }

    private async Task StopPipelineAsync()
    {
        var cancellation = _pipelineCancellation;
        var task = _pipelineTask;
        _pipelineCancellation = null;
        _pipelineTask = null;

        if (cancellation is null || task is null)
        {
            return;
        }

        await cancellation.CancelAsync().ConfigureAwait(false);
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            cancellation.Dispose();
        }
    }

    private async Task RunPipelineGuardedAsync(CancellationToken cancellationToken)
    {
        try
        {
            await RunPipelineAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            SessionCoordinatorLog.PipelineFailed(logger, _session?.Id, exception);
            if (_session is not null)
            {
                _session.Fail(exception.Message, timeProvider.GetUtcNow());
                await sessionRepository.UpdateAsync(_session, CancellationToken.None).ConfigureAwait(false);
            }

            SetState(TranscriptionSessionState.Faulted, "La captura o transcripción se interrumpió.");
            ErrorOccurred?.Invoke(this, exception.Message);
        }
    }

    private async Task RunPipelineAsync(CancellationToken cancellationToken)
    {
        if (_request is null || _session is null)
        {
            throw new InvalidOperationException("The session has not been initialized.");
        }

        audioChunkProcessor.Reset();
        var chunkChannel = Channel.CreateBounded<AudioChunk>(new BoundedChannelOptions(3)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true
        });

        var transcriptionTask = ConsumeChunksAsync(chunkChannel.Reader);
        var lastFrameEnd = TimeSpan.Zero;

        try
        {
            var captureOptions = new AudioCaptureOptions(_request.DeviceId ?? string.Empty);
            await foreach (var frame in audioCaptureSource.CaptureAsync(captureOptions, cancellationToken).ConfigureAwait(false))
            {
                var adjustedFrame = frame with { Start = frame.Start + _captureOffset };
                lastFrameEnd = frame.Start + frame.Duration;
                foreach (var chunk in audioChunkProcessor.Process(adjustedFrame))
                {
                    await chunkChannel.Writer.WriteAsync(chunk, CancellationToken.None).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            var finalChunk = audioChunkProcessor.Flush();
            if (finalChunk is not null)
            {
                await chunkChannel.Writer.WriteAsync(finalChunk, CancellationToken.None).ConfigureAwait(false);
            }

            chunkChannel.Writer.TryComplete();
            await transcriptionTask.ConfigureAwait(false);
            _captureOffset += lastFrameEnd;
        }
    }

    private async Task ConsumeChunksAsync(ChannelReader<AudioChunk> reader)
    {
        await foreach (var chunk in reader.ReadAllAsync(CancellationToken.None).ConfigureAwait(false))
        {
            await foreach (var result in transcriptionProvider.TranscribeAsync(chunk, CancellationToken.None).ConfigureAwait(false))
            {
                var text = result.Text.Trim();
                if (text.Length == 0)
                {
                    continue;
                }

                var absoluteStart = chunk.Start + result.Start;
                var absoluteEnd = chunk.Start + result.End;
                if (absoluteEnd <= _lastCommittedEnd ||
                    string.Equals(text, _lastCommittedText, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (absoluteStart < _lastCommittedEnd)
                {
                    absoluteStart = _lastCommittedEnd;
                }

                var segment = TranscriptSegment.Create(
                    _session!.Id,
                    AudioSourceKind.SystemAudio,
                    text,
                    absoluteStart,
                    absoluteEnd,
                    result.Confidence,
                    timeProvider.GetUtcNow());

                await sessionRepository.AddSegmentAsync(segment, CancellationToken.None).ConfigureAwait(false);
                _lastCommittedEnd = segment.EndTime;
                _lastCommittedText = segment.Text;
                SegmentCommitted?.Invoke(this, new TranscriptSegmentEventArgs(segment));
            }
        }
    }

    private void EnsureSessionState(TranscriptionSessionState expected)
    {
        if (State != expected || _session is null)
        {
            throw new InvalidOperationException($"Expected state {expected}, but current state is {State}.");
        }
    }

    private void SetState(TranscriptionSessionState state, string message)
    {
        State = state;
        StateChanged?.Invoke(this, new SessionStateChangedEventArgs(state, message));
    }

    public async ValueTask DisposeAsync()
    {
        if (_pipelineCancellation is not null)
        {
            await _pipelineCancellation.CancelAsync().ConfigureAwait(false);
        }

        if (_pipelineTask is not null)
        {
            try
            {
                await _pipelineTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _pipelineCancellation?.Dispose();
        _gate.Dispose();
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }
}
