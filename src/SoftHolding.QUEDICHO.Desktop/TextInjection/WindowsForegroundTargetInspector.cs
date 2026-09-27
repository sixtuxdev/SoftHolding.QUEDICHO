using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace SoftHolding.QUEDICHO.Desktop.TextInjection;

internal sealed partial class WindowsForegroundTargetInspector
{
    private readonly int _currentProcessId = Environment.ProcessId;

    public TargetInspection Inspect()
    {
        var windowHandle = GetForegroundWindow();
        if (windowHandle == 0)
        {
            return TargetInspection.Skip("no foreground window");
        }

        GetWindowThreadProcessId(windowHandle, out var processIdValue);
        var processId = checked((int)processIdValue);
        if (processId == _currentProcessId)
        {
            return TargetInspection.Skip("current process");
        }

        try
        {
            var focusedElement = AutomationElement.FocusedElement;
            if (focusedElement is null)
            {
                return TargetInspection.Skip("no focused control");
            }

            var current = focusedElement.Current;
            if (current.ProcessId == _currentProcessId)
            {
                return TargetInspection.Skip("current process");
            }

            if (!current.IsEnabled || !current.HasKeyboardFocus)
            {
                return TargetInspection.Skip("focused control is not available");
            }

            if (current.IsPassword)
            {
                return TargetInspection.Skip("protected field");
            }

            if (!IsEditable(focusedElement, current.ControlType))
            {
                return TargetInspection.Skip("focused control is not editable");
            }

            var effectiveProcessId = current.ProcessId > 0 ? current.ProcessId : processId;
            var processName = GetProcessName(effectiveProcessId);
            var identity = CreateIdentity(windowHandle, effectiveProcessId, focusedElement);
            return TargetInspection.Allow(new TextInjectionTarget(
                windowHandle,
                effectiveProcessId,
                processName,
                identity));
        }
        catch (Exception exception) when (
            exception is ElementNotAvailableException or InvalidOperationException or COMException)
        {
            return TargetInspection.Skip("focused control could not be inspected");
        }
    }

    private static bool IsEditable(AutomationElement element, ControlType controlType)
    {
        if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var valuePatternObject) &&
            valuePatternObject is ValuePattern valuePattern)
        {
            return !valuePattern.Current.IsReadOnly;
        }

        if (controlType == ControlType.Edit)
        {
            return true;
        }

        if (!element.TryGetCurrentPattern(TextPattern.Pattern, out var textPatternObject) ||
            textPatternObject is not TextPattern textPattern)
        {
            return false;
        }

        var readOnlyValue = textPattern.DocumentRange.GetAttributeValue(TextPattern.IsReadOnlyAttribute);
        return readOnlyValue is bool isReadOnly && !isReadOnly;
    }

    private static string GetProcessName(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.ProcessName;
        }
        catch (ArgumentException)
        {
            return "unknown";
        }
    }

    private static string CreateIdentity(nint windowHandle, int processId, AutomationElement focusedElement)
    {
        try
        {
            var runtimeId = focusedElement.GetRuntimeId();
            if (runtimeId is { Length: > 0 })
            {
                return $"{processId}:{windowHandle}:{string.Join('.', runtimeId)}";
            }
        }
        catch (ElementNotAvailableException)
        {
        }

        return $"{processId}:{windowHandle}";
    }

    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(nint windowHandle, out uint processId);
}
