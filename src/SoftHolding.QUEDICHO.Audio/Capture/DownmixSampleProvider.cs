using NAudio.Wave;

namespace SoftHolding.QUEDICHO.Audio.Capture;

internal sealed class DownmixSampleProvider(ISampleProvider source) : ISampleProvider
{
    private readonly int _inputChannels = source.WaveFormat.Channels;
    private float[] _inputBuffer = [];

    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, 1);

    public int Read(float[] buffer, int offset, int count)
    {
        return Read(buffer.AsSpan(offset, count));
    }

    public int Read(Span<float> buffer)
    {
        var needed = buffer.Length * _inputChannels;
        if (_inputBuffer.Length < needed)
        {
            _inputBuffer = new float[needed];
        }

        var samplesRead = source.Read(_inputBuffer.AsSpan(0, needed));
        var framesRead = samplesRead / _inputChannels;
        for (var frame = 0; frame < framesRead; frame++)
        {
            double sum = 0;
            for (var channel = 0; channel < _inputChannels; channel++)
            {
                sum += _inputBuffer[(frame * _inputChannels) + channel];
            }

            buffer[frame] = (float)(sum / _inputChannels);
        }

        return framesRead;
    }
}
