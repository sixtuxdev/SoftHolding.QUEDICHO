using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoftHolding.QUEDICHO.Application.Audio;
using SoftHolding.QUEDICHO.Application.Transcription;
using Whisper.net;
using Whisper.net.Ggml;
using Whisper.net.LibraryLoader;

namespace SoftHolding.QUEDICHO.Transcription.Whisper;

public sealed class LocalWhisperTranscriptionProvider(
    IOptions<WhisperOptions> options,
    ILogger<LocalWhisperTranscriptionProvider> logger) : ITranscriptionProvider, IAsyncDisposable, IDisposable
{
    private readonly WhisperOptions _options = options.Value;
    private readonly SemaphoreSlim _initializationGate = new(1, 1);
    private WhisperFactory? _factory;
    private WhisperProcessor? _processor;

    public string Name => "Whisper Local";

    public event EventHandler<string>? StatusChanged;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_processor is not null)
        {
            return;
        }

        await _initializationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_processor is not null)
            {
                return;
            }

            var modelDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SoftHolding",
                "QUEDICHO",
                "Models");
            Directory.CreateDirectory(modelDirectory);
            var modelPath = Path.Combine(modelDirectory, _options.ModelFileName);

            if (!File.Exists(modelPath) || new FileInfo(modelPath).Length == 0)
            {
                StatusChanged?.Invoke(this, "Descargando el modelo Whisper por primera vez...");
                await DownloadModelAsync(modelPath, cancellationToken).ConfigureAwait(false);
            }

            StatusChanged?.Invoke(this, "Cargando el modelo Whisper...");
            RuntimeOptions.RuntimeLibraryOrder = [RuntimeLibrary.Cpu];
            _factory = WhisperFactory.FromPath(modelPath);
            var threads = _options.Threads > 0
                ? _options.Threads
                : Math.Max(1, Environment.ProcessorCount - 2);
            _processor = _factory.CreateBuilder()
                .WithLanguage(_options.Language)
                .WithThreads(threads)
                .WithProbabilities()
                .Build();
            StatusChanged?.Invoke(this, "Modelo Whisper listo");
            WhisperLog.ModelLoaded(logger, modelPath, _options.Language, threads);
        }
        finally
        {
            _initializationGate.Release();
        }
    }

    public async IAsyncEnumerable<TranscriptionResult> TranscribeAsync(
        AudioChunk chunk,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        if (chunk.Samples.Length == 0)
        {
            yield break;
        }

        var startedAt = System.Diagnostics.Stopwatch.GetTimestamp();
        await foreach (var segment in _processor!.ProcessAsync(chunk.Samples, cancellationToken).ConfigureAwait(false))
        {
            double? probability = double.IsNaN(segment.Probability)
                ? null
                : Math.Clamp((double)segment.Probability, 0, 1);
            yield return new TranscriptionResult(segment.Text, segment.Start, segment.End, probability);
        }

        WhisperLog.ChunkTranscribed(
            logger,
            chunk.Duration.TotalMilliseconds,
            System.Diagnostics.Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
    }

    private async Task DownloadModelAsync(string modelPath, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<GgmlType>(_options.ModelType, true, out var modelType))
        {
            throw new InvalidOperationException($"Unsupported Whisper model type '{_options.ModelType}'.");
        }

        var temporaryPath = modelPath + ".download";
        try
        {
            using var modelStream = await WhisperGgmlDownloader.Default
                .GetGgmlModelAsync(modelType, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            await using (var fileStream = new FileStream(
                             temporaryPath,
                             FileMode.Create,
                             FileAccess.Write,
                             FileShare.None,
                             1024 * 1024,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await modelStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
                await fileStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, modelPath, true);
        }
        catch
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_processor is not null)
        {
            await _processor.DisposeAsync().ConfigureAwait(false);
        }

        _factory?.Dispose();
        _initializationGate.Dispose();
    }

    public void Dispose()
    {
        _processor?.Dispose();
        _factory?.Dispose();
        _initializationGate.Dispose();
        GC.SuppressFinalize(this);
    }
}
