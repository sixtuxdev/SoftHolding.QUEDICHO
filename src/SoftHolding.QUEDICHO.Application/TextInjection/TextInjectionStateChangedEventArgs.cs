namespace SoftHolding.QUEDICHO.Application.TextInjection;

public sealed class TextInjectionStateChangedEventArgs(bool isEnabled) : EventArgs
{
    public bool IsEnabled { get; } = isEnabled;
}
