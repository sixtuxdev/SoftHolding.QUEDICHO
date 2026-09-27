using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Extensions.Logging;
using SoftHolding.QUEDICHO.Application.TextInjection;

namespace SoftHolding.QUEDICHO.Desktop.TextInjection;

internal sealed partial class WindowsGlobalHotkeyService(
    ITextInjectionService textInjectionService,
    ILogger<WindowsGlobalHotkeyService> logger) : IGlobalHotkeyService
{
    private const int HotkeyId = 0x5144;
    private const int WindowsMessageHotkey = 0x0312;
    private const uint ModifierAlt = 0x0001;
    private const uint ModifierControl = 0x0002;
    private const uint ModifierShift = 0x0004;
    private const uint ModifierWindows = 0x0008;
    private const uint ModifierNoRepeat = 0x4000;

    public bool IsRegistered { get; private set; }

    public event EventHandler? Pressed;

    public bool Register()
    {
        if (IsRegistered)
        {
            return true;
        }

        if (!TryParseGesture(textInjectionService.GlobalHotkey, out var modifiers, out var virtualKey))
        {
            TextInjectionLog.HotkeyRegistrationFailed(logger, textInjectionService.GlobalHotkey, 87);
            return false;
        }

        ComponentDispatcher.ThreadPreprocessMessage += OnThreadMessage;
        if (!RegisterHotKey(0, HotkeyId, modifiers | ModifierNoRepeat, virtualKey))
        {
            ComponentDispatcher.ThreadPreprocessMessage -= OnThreadMessage;
            TextInjectionLog.HotkeyRegistrationFailed(
                logger,
                textInjectionService.GlobalHotkey,
                Marshal.GetLastPInvokeError());
            return false;
        }

        IsRegistered = true;
        TextInjectionLog.HotkeyRegistered(logger, textInjectionService.GlobalHotkey);
        return true;
    }

    public void Unregister()
    {
        if (!IsRegistered)
        {
            return;
        }

        UnregisterHotKey(0, HotkeyId);
        ComponentDispatcher.ThreadPreprocessMessage -= OnThreadMessage;
        IsRegistered = false;
        TextInjectionLog.HotkeyUnregistered(logger, textInjectionService.GlobalHotkey);
    }

    private void OnThreadMessage(ref MSG message, ref bool handled)
    {
        if (message.message != WindowsMessageHotkey || message.wParam != HotkeyId)
        {
            return;
        }

        handled = true;
        Pressed?.Invoke(this, EventArgs.Empty);
    }

    private static bool TryParseGesture(string gesture, out uint modifiers, out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;
        var tokens = gesture.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var token in tokens)
        {
            if (token.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) ||
                token.Equals("Control", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= ModifierControl;
            }
            else if (token.Equals("Shift", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= ModifierShift;
            }
            else if (token.Equals("Alt", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= ModifierAlt;
            }
            else if (token.Equals("Win", StringComparison.OrdinalIgnoreCase) ||
                     token.Equals("Windows", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= ModifierWindows;
            }
            else if (virtualKey == 0 && TryParseKey(token, out var parsedKey))
            {
                virtualKey = parsedKey;
            }
            else
            {
                return false;
            }
        }

        return modifiers != 0 && virtualKey != 0;
    }

    private static bool TryParseKey(string token, out uint virtualKey)
    {
        if (token.Length == 1 && char.IsAsciiLetterOrDigit(token[0]))
        {
            virtualKey = char.ToUpperInvariant(token[0]);
            return true;
        }

        if (Enum.TryParse<Key>(token, true, out var key) && key != Key.None)
        {
            virtualKey = checked((uint)KeyInterop.VirtualKeyFromKey(key));
            return virtualKey != 0;
        }

        virtualKey = 0;
        return false;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterHotKey(nint windowHandle, int id, uint modifiers, uint virtualKey);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterHotKey(nint windowHandle, int id);
}
