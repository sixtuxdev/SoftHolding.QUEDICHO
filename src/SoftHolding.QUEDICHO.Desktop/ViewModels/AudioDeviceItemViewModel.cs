using SoftHolding.QUEDICHO.Application.Audio;

namespace SoftHolding.QUEDICHO.Desktop.ViewModels;

public sealed record AudioDeviceItemViewModel(string Id, string Name, bool IsDefault)
{
    public string DisplayName => IsDefault ? $"{Name} (predeterminado)" : Name;

    public static AudioDeviceItemViewModel FromModel(AudioDeviceInfo model) =>
        new(model.Id, model.Name, model.IsDefault);
}
