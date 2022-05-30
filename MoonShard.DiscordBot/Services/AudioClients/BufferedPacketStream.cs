using System.Buffers;
using System.Diagnostics;
using System.Threading.Channels;
using Discord.Audio;
using Discord.Audio.Streams;
using LibavSharp.Core.AVCodec;

namespace MoonShard.DiscordBot.AudioServices;

public class BufferedPacketStream
{
    public const int DefaultQueueDepth = 50;

    public const int DefaultPacketDurationMs = 20;

    private const int SilencePacketBucketMax = 5;

    public BufferedPacketStream(AudioOutStream directOpusStream) : this(DefaultQueueDepth, DefaultPacketDurationMs,
        directOpusStream)
    {
    }

    private BufferedPacketStream(int bufferSize, int packetDurationMs, AudioOutStream directOpusStream)
    {
        if (directOpusStream is not RTPWriteStream)
            throw new ArgumentException("Opus packets are expected to be sent through a RTPWriteStream.");

        PacketDurationMs = packetDurationMs;
        OutputStream = directOpusStream;

        var queue = Channel.CreateBounded<Element>(new BoundedChannelOptions(DefaultQueueDepth)
        {
            FullMode = BoundedChannelFullMode.Wait
        });
        QueueReader = queue.Reader;
        QueueWriter = queue.Writer;
    }

    private AudioOutStream OutputStream { get; }

    private int PacketDurationMs { get; }

    private ChannelReader<Element> QueueReader { get; }

    private ChannelWriter<Element> QueueWriter { get; }

    private int SilencePacketBucket { get; set; } = SilencePacketBucketMax;

    private static byte[] Silence { get; } = {0xF8, 0xFF, 0xFE};

    private CancellationTokenSource StopTokenSource { get; } = new();

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        var token = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, StopTokenSource.Token).Token;

        // spin until queue is empty

        while (!token.IsCancellationRequested && QueueReader.Count > 0)
            await Task.Delay(QueueReader.Count * DefaultPacketDurationMs, cancellationToken);

        token.ThrowIfCancellationRequested();
    }

    public void Start()
    {
        new Thread(Run).Start();
    }

    public void Stop()
    {
        QueueWriter.TryComplete();
        StopTokenSource.Cancel();
    }

    public async ValueTask WriteAsync(AVPacket packet, CancellationToken cancellationToken = default)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(packet.Size);
        packet.CopyTo(buffer);

        try
        {
            await QueueWriter.WriteAsync(new Element(buffer, packet.Size), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (ChannelClosedException)
        {
            // TODO: packets will be black holed if the channel is completed
        }
    }

    /// <param name="stopToken"><see cref="CancellationToken" /> to stop sending packets and exit the thread.</param>
    private void Run()
    {
        var stopToken = StopTokenSource.Token;

        var stopwatch = new Stopwatch();
        stopwatch.Start();

        var sequence = 0;
        var timestamp = stopwatch.ElapsedMilliseconds;
        var nextTimestamp = timestamp + PacketDurationMs - timestamp % PacketDurationMs;

        while (!stopToken.IsCancellationRequested)
        {
            // fast-forward timestamp of next scheduled packet if can't keep up with the clock

            var skippedTime = Math.Max(stopwatch.ElapsedMilliseconds - nextTimestamp, 0);
            var skippedPackets = (int) Math.Truncate((double) skippedTime / PacketDurationMs);

            if (skippedPackets > 0)
            {
                sequence += skippedPackets;
                nextTimestamp += skippedPackets * PacketDurationMs;

                if (SilencePacketBucket > 0)
                    Console.WriteLine($"Can't keep up with the clock. Skipped {skippedPackets} packets.");
            }

            // sleep until the next packet should be sent

            timestamp = stopwatch.ElapsedMilliseconds;
            while (timestamp < nextTimestamp)
            {
                Thread.Sleep((int) Math.Clamp(nextTimestamp - timestamp, 0, DefaultPacketDurationMs));
                timestamp = stopwatch.ElapsedMilliseconds;
            }

            nextTimestamp += PacketDurationMs;

            // send the current packet

            var packetSequence = (ushort) (++sequence % ushort.MaxValue);
            var packetTimestamp = (uint) ((double) (timestamp - timestamp % PacketDurationMs) * 48000 / 1000);

            if (QueueReader.TryRead(out var queuedPacket) && queuedPacket is {Buffer: var buffer, Size: var size})
            {
                // reset silence packet bucket 
                SilencePacketBucket = SilencePacketBucketMax;

                // send out the packet
                OutputStream.WriteHeader(packetSequence, packetTimestamp, skippedPackets > 0);
                OutputStream.WriteAsync(buffer, 0, size, stopToken).ConfigureAwait(false);

                // return the buffer to the pool
                ArrayPool<byte>.Shared.Return(buffer);
            }
            else
            {
                // send five silence packets to avoid unintended Opus interpolation
                // see https://discord.com/developers/docs/topics/voice-connections#voice-data-interpolation

                if (SilencePacketBucket <= 0)
                {
                    // wait for the next packet to be queued after sending five silence packets
                    if (!QueueReader.WaitToReadAsync(stopToken).AsTask().GetAwaiter().GetResult())
                        // WaitToReadAsync() returns false if channel is closed
                        break;
                }
                else
                {
                    SilencePacketBucket--;

                    OutputStream.WriteHeader(packetSequence, packetTimestamp, skippedPackets > 0);
                    OutputStream.WriteAsync(Silence, 0, Silence.Length, stopToken).ConfigureAwait(false);
                }
            }
        }

        // don't throw for stopToken, it's an expected cancellation 
    }

    private struct Element
    {
        public byte[] Buffer { get; }

        public int Size { get; }

        public Element(byte[] buffer, int size)
        {
            Buffer = buffer;
            Size = size;
        }
    }
}