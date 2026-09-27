using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SoftHolding.QUEDICHO.Application.TextInjection;

namespace SoftHolding.QUEDICHO.Desktop.TextInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddWindowsTextInjection(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<TextInjectionOptions>()
            .Bind(configuration.GetSection(TextInjectionOptions.SectionName));
        services.AddSingleton<TextInjectionPreferencesStore>();
        services.AddSingleton<WindowsForegroundTargetInspector>();
        services.AddSingleton<StaClipboardService>();
        services.AddSingleton<SendInputUnicodeStrategy>();
        services.AddSingleton<ClipboardPasteTextInputStrategy>();
        services.AddSingleton<CompositeTextInjector>();
        services.AddSingleton<WindowsTextInjectionService>();
        services.AddSingleton<ITextInjectionService>(provider =>
            provider.GetRequiredService<WindowsTextInjectionService>());
        services.AddSingleton<IHostedService>(provider =>
            provider.GetRequiredService<WindowsTextInjectionService>());
        services.AddSingleton<IGlobalHotkeyService, WindowsGlobalHotkeyService>();
        return services;
    }
}
