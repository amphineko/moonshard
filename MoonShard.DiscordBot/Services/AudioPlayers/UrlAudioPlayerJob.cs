using System.Threading.Channels;
using LibavSharp.Core.AVCodec;
using LibavSharp.Core.AVFormat;
using LibavSharp.Core.AVUtil;
using LibavSharp.Extensions.Demuxing;
using LibavSharp.Extensions.Muxing;
using LibavSharp.Extensions.Resampling;
using MoonShard.DiscordBot.AudioServices;
using MoonShard.DiscordBot.Common;

namespace MoonShard.DiscordBot.Services.AudioPlayers;

public abstract class UrlAudioPlayerJob
{
    protected UrlAudioPlayerJob(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public abstract Task PlayAsync(int bitRate, BufferedPacketStream outputStream, AsyncPauseToken pauseToken,
        CancellationToken cancellationToken);

    /// <remarks>
    ///     A new Task is created to prevent blocking the caller, as network operations may take a while.
    /// </remarks>
    private static Task<(AVFormatContext Format, AVStream Stream)> OpenInputAsync(string url)
    {
        return Task.Run(() =>
        {
            AVFormatContext? format = null;
            AVStream? stream;

            try
            {
                format = AVFormatContext.OpenInput(url);
                stream = format.FindBestOrFirstStream(AVMediaType.Audio);

                if (stream == null)
                    throw new IOException("No audio stream found");

                FormatReader.DiscardUnusedStreams(new List<int> {stream.Index}, format);

                return (format, stream);
            }
            catch (Exception e)
            {
                format?.Dispose();
                throw new InternalPlaybackException($"Error while opening input: {e.Message}", e);
            }
        });
    }

    private static Task<AVCodecContext> OpenOutputAsync(int bitRate, int packetDuration)
    {
        return Task.Run(() =>
        {
            AVCodecContext? encoder = null;

            try
            {
                const AVCodecId outputCodecId = AVCodecId.Opus;
                using var outputCodec = AVCodec.FindEncoder((int) outputCodecId);

                using var codecParams = AVCodecParameters.Create();
                codecParams.CodecId = (int) outputCodecId;
                codecParams.CodecType = AVMediaType.Audio;
                codecParams.BitRate = bitRate;
                codecParams.Channels = 2;
                codecParams.ChannelLayout = (ulong) AVChannelLayout.GetDefaultChannelLayout(codecParams.Channels);
                codecParams.SampleFormat = outputCodec.GetSupportedSampleFormats()[0];
                codecParams.SampleRate = 48000;

                using var encoderOptions = new AVDictionary();
                encoderOptions.Set("application", "audio");
                encoderOptions.Set("frame_duration", packetDuration.ToString());
                encoderOptions.Set("fec", "1");
                encoderOptions.Set("packet_loss", "0");

                encoder = new AVCodecContext(outputCodec)
                {
                    StandardCompliance = StandardCompliance.Experimental
                };
                codecParams.CopyToContext(encoder);
                encoder.Open(encoderOptions);

                return encoder;
            }
            catch (Exception e)
            {
                encoder?.Dispose();
                throw new InternalPlaybackException($"Error while opening encoder: {e.Message}", e);
            }
        });
    }

    protected static async Task PlayAsync(string url, int bitRate, BufferedPacketStream outputStream,
        AsyncPauseToken pauseToken, CancellationToken stopToken)
    {
        // await a immediate task to prevent blocking the caller thread

        await Task.Delay(TimeSpan.Zero, CancellationToken.None);

        // input setup 

        var inputTuple = await OpenInputAsync(url);

        using var input = inputTuple.Format;
        var inputStream = inputTuple.Stream; // AVStream doesn't require disposal

        // output setup

        const int packetDuration = BufferedPacketStream.DefaultPacketDurationMs;
        using var encoder = await OpenOutputAsync(bitRate, packetDuration);

        // transcode to opus

        var inputPackets = Channel.CreateUnbounded<AVPacket>();
        var inputFrames = Channel.CreateUnbounded<AVFrame>();
        var outputFrames = Channel.CreateUnbounded<AVFrame>();
        var outputPackets = Channel.CreateUnbounded<AVPacket>();

        var inputPacketDestinations = new Dictionary<int, ChannelWriter<AVPacket>>
        {
            {inputStream.Index, inputPackets.Writer}
        };

        var resampleFramer = new ResampleFramer(outputFrames.Writer, inputFrames.Reader, encoder);

        await Task.WhenAll(
            // read encoded packets from input format (i.e. container)
            FormatReader
                .ReadPacketsAsync(inputPacketDestinations, input, stopToken)
                .ContinueWith(task =>
                {
                    if (task.Exception?.GetBaseException() is {Message: { } message} ex)
                        throw new ExternalPlaybackException($"Error while reading remote media: {message}", ex);
                }, TaskContinuationOptions.None),

            // decode packets to audio frames
            StreamDecoder
                .DecodeFramesAsync(inputFrames.Writer, inputPackets.Reader, inputStream, stopToken)
                .ContinueWith(task =>
                {
                    if (task.Exception?.GetBaseException() is {Message: { } message} ex)
                        throw new ExternalPlaybackException($"Error while decoding remote stream: {message}", ex);
                }, TaskContinuationOptions.None),

            // resample frames to output format (discord opus requires 48kHz, while input may be different)
            resampleFramer
                .RunAsync(stopToken)
                .ContinueWith(task =>
                {
                    if (task.Exception?.GetBaseException() is {Message: { } message} ex)
                        throw new InternalPlaybackException($"Error while resampling audio stream: {message}", ex);
                }, TaskContinuationOptions.None),

            // encode frames to packets (to be sent to discord)
            StreamEncoder
                .EncodeFramesAsync(outputPackets.Writer, outputFrames.Reader, encoder, stopToken)
                .ContinueWith(task =>
                {
                    if (task.Exception?.GetBaseException() is {Message: { } message} ex)
                        throw new InternalPlaybackException($"Error while encoding audio output: {message}", ex);
                }, TaskContinuationOptions.None),

            // send packets to buffer (which will send packets to discord with correct timing)
            Task.Run(async () =>
                {
                    var packetReader = outputPackets.Reader;
                    while (await packetReader.WaitToReadAsync(stopToken))
                    {
                        while (pauseToken.IsPausedRequested) await pauseToken.WaitWhilePausedAsync();

                        using var packet = await packetReader.ReadAsync(stopToken);
                        try
                        {
                            await outputStream.WriteAsync(packet, CancellationToken.None);
                        }
                        catch (EndOfStreamException)
                        {
                            return;
                        }
                    }

                    Console.WriteLine("End of stream reached");
                }, CancellationToken.None)
                .ContinueWith(async task =>
                {
                    if (task.Exception?.GetBaseException() is {Message: { } message} ex)
                        throw new InternalPlaybackException($"Error while writing audio buffer: {message}", ex);

                    await outputStream.FlushAsync(stopToken);
                }, TaskContinuationOptions.None)
        );
    }
}