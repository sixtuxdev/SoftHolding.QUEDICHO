namespace SoftHolding.QUEDICHO.Domain.Sessions;

public enum SessionStatus
{
    Created = 0,
    Starting = 1,
    Listening = 2,
    Paused = 3,
    Stopping = 4,
    Completed = 5,
    Interrupted = 6,
    Faulted = 7
}
