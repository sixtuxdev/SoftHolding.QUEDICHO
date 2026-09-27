using System.Runtime.InteropServices;

namespace SoftHolding.QUEDICHO.Desktop.TextInjection;

internal static partial class WindowsKeyboardInput
{
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint KeyEventUnicode = 0x0004;
    private const int VirtualKeyControl = 0x11;
    private const int VirtualKeyShift = 0x10;
    private const int VirtualKeyMenu = 0x12;
    private const int VirtualKeyLeftWindows = 0x5B;
    private const int VirtualKeyRightWindows = 0x5C;
    private const ushort VirtualKeyV = 0x56;

    public static async ValueTask<TextInjectionAttempt> SendUnicodeTextAsync(
        TextInjectionTarget target,
        string text,
        CancellationToken cancellationToken)
    {
        var readiness = await WaitUntilReadyAsync(target, cancellationToken).ConfigureAwait(false);
        if (readiness is not null)
        {
            return readiness;
        }

        var inputs = new Input[text.Length * 2];
        for (var index = 0; index < text.Length; index++)
        {
            inputs[index * 2] = CreateUnicodeInput(text[index], keyUp: false);
            inputs[(index * 2) + 1] = CreateUnicodeInput(text[index], keyUp: true);
        }

        return Send(inputs);
    }

    public static async ValueTask<TextInjectionAttempt> SendPasteShortcutAsync(
        TextInjectionTarget target,
        CancellationToken cancellationToken)
    {
        var readiness = await WaitUntilReadyAsync(target, cancellationToken).ConfigureAwait(false);
        if (readiness is not null)
        {
            return readiness;
        }

        Input[] inputs =
        [
            CreateVirtualKeyInput(VirtualKeyControl, keyUp: false),
            CreateVirtualKeyInput(VirtualKeyV, keyUp: false),
            CreateVirtualKeyInput(VirtualKeyV, keyUp: true),
            CreateVirtualKeyInput(VirtualKeyControl, keyUp: true)
        ];
        return Send(inputs);
    }

    private static async ValueTask<TextInjectionAttempt?> WaitUntilReadyAsync(
        TextInjectionTarget target,
        CancellationToken cancellationToken)
    {
        var timeoutAt = TimeProvider.System.GetTimestamp() +
                        (long)(TimeProvider.System.TimestampFrequency * 0.75);
        while (AreModifiersPressed())
        {
            if (TimeProvider.System.GetTimestamp() >= timeoutAt)
            {
                return new TextInjectionAttempt(
                    TextInjectionAttemptStatus.RetryableFailure,
                    "keyboard modifiers remained pressed");
            }

            await Task.Delay(20, cancellationToken).ConfigureAwait(false);
        }

        return GetForegroundWindow() == target.WindowHandle
            ? null
            : new TextInjectionAttempt(TextInjectionAttemptStatus.TargetChanged, "foreground target changed");
    }

    private static bool AreModifiersPressed() =>
        IsPressed(VirtualKeyControl) ||
        IsPressed(VirtualKeyShift) ||
        IsPressed(VirtualKeyMenu) ||
        IsPressed(VirtualKeyLeftWindows) ||
        IsPressed(VirtualKeyRightWindows);

    private static bool IsPressed(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    private static TextInjectionAttempt Send(Input[] inputs)
    {
        if (inputs.Length == 0)
        {
            return TextInjectionAttempt.Success();
        }

        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        if (sent == inputs.Length)
        {
            return TextInjectionAttempt.Success();
        }

        var error = Marshal.GetLastPInvokeError();
        return sent == 0
            ? new TextInjectionAttempt(
                TextInjectionAttemptStatus.RetryableFailure,
                $"SendInput inserted no events (Win32 error {error})")
            : new TextInjectionAttempt(
                TextInjectionAttemptStatus.Failed,
                $"SendInput inserted only {sent} of {inputs.Length} events (Win32 error {error})");
    }

    private static Input CreateUnicodeInput(char character, bool keyUp) => new()
    {
        Type = InputKeyboard,
        Data = new InputUnion
        {
            Keyboard = new KeyboardInput
            {
                Scan = character,
                Flags = KeyEventUnicode | (keyUp ? KeyEventKeyUp : 0)
            }
        }
    };

    private static Input CreateVirtualKeyInput(ushort virtualKey, bool keyUp) => new()
    {
        Type = InputKeyboard,
        Data = new InputUnion
        {
            Keyboard = new KeyboardInput
            {
                VirtualKey = virtualKey,
                Flags = keyUp ? KeyEventKeyUp : 0
            }
        }
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInput Mouse;

        [FieldOffset(0)]
        public KeyboardInput Keyboard;

        [FieldOffset(0)]
        public HardwareInput Hardware;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HardwareInput
    {
        public uint Message;
        public ushort ParameterLow;
        public ushort ParameterHigh;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial uint SendInput(uint numberOfInputs, [In] Input[] inputs, int sizeOfInput);

    [LibraryImport("user32.dll")]
    private static partial short GetAsyncKeyState(int virtualKey);

    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();
}
