namespace SoftHolding.QUEDICHO.Desktop.TextInjection;

public sealed class TextInjectionOptions
{
    public const string SectionName = "TextInjection";

    public bool Enabled { get; set; }
    public string GlobalHotkey { get; set; } = "Ctrl+Shift+Y";
    public int QueueCapacity { get; set; } = 128;
    public int ClipboardRestoreDelayMilliseconds { get; set; } = 120;
}
