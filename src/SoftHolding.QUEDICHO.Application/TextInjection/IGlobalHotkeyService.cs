namespace SoftHolding.QUEDICHO.Application.TextInjection;

public interface IGlobalHotkeyService
{
    bool IsRegistered { get; }

    event EventHandler? Pressed;

    bool Register();
    void Unregister();
}
