namespace SoftHolding.QUEDICHO.Application.Sessions;

public enum TranscriptionSessionState
{
    Idle,
    Preparing,
    Listening,
    Paused,
    Stopping,
    Completed,
    Faulted
}
