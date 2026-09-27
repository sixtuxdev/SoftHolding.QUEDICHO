using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using SoftHolding.QUEDICHO.Application.Audio;
using SoftHolding.QUEDICHO.Audio.Devices;

namespace SoftHolding.QUEDICHO.Audio.Capture;

public sealed class WasapiLoopbackCaptureSource(
    WasapiAudioDeviceCatalog deviceCatalog,
    ILogger<WasapiLoopbackCaptureSource> logger) : IAudioCaptureSource
{
    private sealed record RawPacket(byte[] Buffer, int Count);

    public async IAsyncEnumerable<AudioFrame> CaptureAsync(
        AudioCaptureOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var absoluteSamplePosition = 0L;

        while (!cancellationToken.IsCancellationRequested)
        {
            var restartRequested = 0;
            var rawChannel = Channel.CreateBounded<RawPacket>(new BoundedChannelOptions(256)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = true
            });

            using var device = deviceCatalog.GetDevice(options.DeviceId);
            using var capture = new WasapiRecorderBuilder()
                .WithDevice(device)
                .WithSharedMode()
                .WithLoopbackCapture()
                .Build();
            EventHandler devicesChanged = (_, _) =>
            {
                if (Interlocked.Exchange(ref restartRequested, 1) != 0)
                {
                    return;
                }

                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        capture.StopRecording();
                    }
                    catch (InvalidOperationException)
                    {
                    }
                });
            };

            deviceCatalog.DevicesChanged += devicesChanged;
            capture.DataAvailable += (buffer, _, _, _) =>
            {
                if (buffer.IsEmpty)
                {
                    return;
                }

                var rented = ArrayPool<byte>.Shared.Rent(buffer.Length);
                buffer.CopyTo(rented);
                if (!rawChannel.Writer.TryWrite(new RawPacket(rented, buffer.Length)))
                {
                    ArrayPool<byte>.Shared.Return(rented);
                    AudioCaptureLog.BufferOverflow(logger);
                }
            };
            capture.RecordingStopped += (_, eventArgs) => rawChannel.Writer.TryComplete(eventArgs.Exception);

            using var cancellationRegistration = cancellationToken.Register(() =>
            {
                try
                {
                    capture.StopRecording();
                }
                catch (InvalidOperationException)
                {
                }
            });

            var bufferedProvider = new BufferedWaveProvider(capture.WaveFormat, TimeSpan.FromSeconds(5))
            {
                DiscardOnBufferOverflow = false,
                ReadFully = false
            };
            ISampleProvider sampleProvider = bufferedProvider.ToSampleProvider();
            if (sampleProvider.WaveFormat.Channels > 1)
            {
                sampleProvider = new DownmixSampleProvider(sampleProvider);
            }

            var resampler = new WdlResamplingSampleProvider(sampleProvider, options.TargetSampleRate);
            var frameSize = options.TargetSampleRate * options.FrameDurationMilliseconds / 1000;
            var resampledBuffer = new float[Math.Max(frameSize * 4, 4096)];
            var frameBuffer = new float[frameSize];
            var framePosition = 0;

            try
            {
                AudioCaptureLog.CaptureStarted(logger, device.FriendlyName, capture.WaveFormat.ToString());
                capture.StartRecording();

                await foreach (var packet in rawChannel.Reader.ReadAllAsync(CancellationToken.None).ConfigureAwait(false))
                {
                    try
                    {
                        bufferedProvider.AddSamples(packet.Buffer, 0, packet.Count);
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(packet.Buffer);
                    }

                    while (bufferedProvider.BufferedBytes > 0)
                    {
                        var read = resampler.Read(resampledBuffer);
                        if (read == 0)
                        {
                            break;
                        }

                        var sourceOffset = 0;
                        while (sourceOffset < read)
                        {
                            var copyCount = Math.Min(frameSize - framePosition, read - sourceOffset);
                            Array.Copy(resampledBuffer, sourceOffset, frameBuffer, framePosition, copyCount);
                            framePosition += copyCount;
                            sourceOffset += copyCount;

                            if (framePosition == frameSize)
                            {
                                var completedFrame = frameBuffer;
                                var start = TimeSpan.FromSeconds((double)absoluteSamplePosition / options.TargetSampleRate);
                                absoluteSamplePosition += completedFrame.Length;
                                frameBuffer = new float[frameSize];
                                framePosition = 0;
                                yield return new AudioFrame(completedFrame, options.TargetSampleRate, start);
                            }
                        }
                    }
                }
            }
            finally
            {
                deviceCatalog.DevicesChanged -= devicesChanged;
                while (rawChannel.Reader.TryRead(out var pendingPacket))
                {
                    ArrayPool<byte>.Shared.Return(pendingPacket.Buffer);
                }

                if (capture.CaptureState != CaptureState.Stopped)
                {
                    capture.StopRecording();
                }
            }

            if (restartRequested == 0 || cancellationToken.IsCancellationRequested)
            {
                break;
            }

            AudioCaptureLog.EndpointRestart(logger);
            await Task.Delay(300, cancellationToken).ConfigureAwait(false);
        }
    }
}
