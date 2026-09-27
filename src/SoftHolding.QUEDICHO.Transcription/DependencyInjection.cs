using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SoftHolding.QUEDICHO.Application.Transcription;
using SoftHolding.QUEDICHO.Transcription.Whisper;

namespace SoftHolding.QUEDICHO.Transcription;

public static class DependencyInjection
{
    public static IServiceCollection AddLocalWhisperTranscription(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<WhisperOptions>()
            .Bind(configuration.GetSection(WhisperOptions.SectionName));
        services.AddSingleton<ITranscriptionProvider, LocalWhisperTranscriptionProvider>();
        return services;
    }
}
