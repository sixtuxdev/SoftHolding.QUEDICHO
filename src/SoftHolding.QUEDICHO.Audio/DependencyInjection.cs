using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SoftHolding.QUEDICHO.Application.Audio;
using SoftHolding.QUEDICHO.Audio.Capture;
using SoftHolding.QUEDICHO.Audio.Devices;
using SoftHolding.QUEDICHO.Audio.Processing;

namespace SoftHolding.QUEDICHO.Audio;

public static class DependencyInjection
{
    public static IServiceCollection AddWindowsAudio(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AudioProcessingOptions>()
            .Bind(configuration.GetSection(AudioProcessingOptions.SectionName));
        services.AddSingleton<WasapiAudioDeviceCatalog>();
        services.AddSingleton<IAudioDeviceCatalog>(provider => provider.GetRequiredService<WasapiAudioDeviceCatalog>());
        services.AddSingleton<IAudioCaptureSource, WasapiLoopbackCaptureSource>();
        services.AddSingleton<IAudioChunkProcessor, AdaptiveVoiceChunkProcessor>();
        return services;
    }
}
