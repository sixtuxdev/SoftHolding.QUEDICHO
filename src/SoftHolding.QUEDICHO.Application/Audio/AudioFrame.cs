namespace SoftHolding.QUEDICHO.Application.Audio;

public sealed record AudioFrame(float[] Samples, int SampleRate, TimeSpan Start)
{
    public TimeSpan Duration => TimeSpan.FromSeconds((double)Samples.Length / SampleRate);
}
