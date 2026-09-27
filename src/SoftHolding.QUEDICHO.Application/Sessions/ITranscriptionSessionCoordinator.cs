namespace SoftHolding.QUEDICHO.Application.Sessions;

public interface ITranscriptionSessionCoordinator
{
    event EventHandler<SessionStateChangedEventArgs>? StateChanged;
    event EventHandler<TranscriptSegmentEventArgs>? SegmentCommitted;
    event EventHandler<string>? ErrorOccurred;

    TranscriptionSessionState State { get; }
    Guid? CurrentSessionId { get; }

    Task StartAsync(StartSessionRequest request, CancellationToken cancellationToken = default);
    Task PauseAsync(CancellationToken cancellationToken = default);
    Task ResumeAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}
