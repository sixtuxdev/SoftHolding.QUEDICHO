using Microsoft.Extensions.Options;
using SoftHolding.QUEDICHO.Application.Audio;

namespace SoftHolding.QUEDICHO.Audio.Processing;

public sealed class AdaptiveVoiceChunkProcessor(IOptions<AudioProcessingOptions> options) : IAudioChunkProcessor
{
    private readonly AudioProcessingOptions _options = options.Value;
    private readonly Queue<AudioFrame> _preRoll = new();
    private readonly List<float> _currentSamples = [];
    private bool _recording;
    private int _silenceMilliseconds;
    private int _speechMilliseconds;
    private int _sampleRate = 16_000;
    private TimeSpan _chunkStart;

    public void Reset()
    {
        _preRoll.Clear();
        _currentSamples.Clear();
        _recording = false;
        _silenceMilliseconds = 0;
        _speechMilliseconds = 0;
        _chunkStart = TimeSpan.Zero;
    }

    public IReadOnlyList<AudioChunk> Process(AudioFrame frame)
    {
        var result = new List<AudioChunk>(1);
        _sampleRate = frame.SampleRate;
        var frameMilliseconds = Math.Max(1, (int)Math.Round(frame.Duration.TotalMilliseconds));
        var isSpeech = CalculateDecibels(frame.Samples) >= _options.SpeechThresholdDb;

        if (!_recording)
        {
            _preRoll.Enqueue(frame);
            TrimPreRoll();
            if (!isSpeech)
            {
                return result;
            }

            _recording = true;
            _chunkStart = _preRoll.Peek().Start;
            while (_preRoll.TryDequeue(out var preRollFrame))
            {
                _currentSamples.AddRange(preRollFrame.Samples);
            }

            _speechMilliseconds = frameMilliseconds;
            _silenceMilliseconds = 0;
            return result;
        }

        _currentSamples.AddRange(frame.Samples);
        if (isSpeech)
        {
            _speechMilliseconds += frameMilliseconds;
            _silenceMilliseconds = 0;
        }
        else
        {
            _silenceMilliseconds += frameMilliseconds;
        }

        var currentDurationMilliseconds = _currentSamples.Count * 1000d / frame.SampleRate;
        if ((_silenceMilliseconds >= _options.SilenceToCloseMilliseconds &&
             _speechMilliseconds >= _options.MinimumSpeechMilliseconds) ||
            currentDurationMilliseconds >= _options.MaximumChunkMilliseconds)
        {
            result.Add(CreateChunk(frame.SampleRate));
            Reset();
        }

        return result;
    }

    public AudioChunk? Flush()
    {
        if (!_recording || _speechMilliseconds < _options.MinimumSpeechMilliseconds || _currentSamples.Count == 0)
        {
            Reset();
            return null;
        }

        var chunk = CreateChunk(_sampleRate);
        Reset();
        return chunk;
    }

    private AudioChunk CreateChunk(int sampleRate) => new(_currentSamples.ToArray(), sampleRate, _chunkStart);

    private void TrimPreRoll()
    {
        var duration = _preRoll.Sum(frame => frame.Duration.TotalMilliseconds);
        while (duration > _options.PreRollMilliseconds && _preRoll.TryDequeue(out var removed))
        {
            duration -= removed.Duration.TotalMilliseconds;
        }
    }

    private static double CalculateDecibels(float[] samples)
    {
        if (samples.Length == 0)
        {
            return double.NegativeInfinity;
        }

        double sumSquares = 0;
        foreach (var sample in samples)
        {
            sumSquares += sample * sample;
        }

        var rms = Math.Sqrt(sumSquares / samples.Length);
        return rms <= 0.000_001 ? -120 : 20 * Math.Log10(rms);
    }
}
