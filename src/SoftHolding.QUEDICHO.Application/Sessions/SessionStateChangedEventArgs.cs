namespace SoftHolding.QUEDICHO.Application.Sessions;

public sealed class SessionStateChangedEventArgs(TranscriptionSessionState state, string message) : EventArgs
{
    public TranscriptionSessionState State { get; } = state;
    public string Message { get; } = message;
}
