using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using MoonShard.DiscordBot.ExternalServices.Bilibili;
using MoonShard.DiscordBot.ExternalServices.NeteaseMusic;
using MoonShard.DiscordBot.Services.AudioPlayers;

namespace MoonShard.DiscordBot.Commands;

public class AudioPlayerCommands : InteractionModuleBase
{
    public AudioPlayerCommands(AudioPlayerRepository players, NeteaseMusicJobFactory neteaseMusicJobFactory)
    {
        Players = players;
        NeteaseMusicJobFactory = neteaseMusicJobFactory;
    }

    private AudioPlayerRepository Players { get; }

    private NeteaseMusicJobFactory NeteaseMusicJobFactory { get; }

    private static HttpClient HttpClient { get; } = new();

    [SlashCommand("bilibili", "Play a bilibili video from a given url")]
    public async Task QueueBilibiliAsync(string url)
    {
        if (Context.Interaction is not SocketInteraction {Channel: var textChannel})
        {
            await ReplyAsync("This command can only be invoked in a text channel.");
            return;
        }

        try
        {
            await EnsureConnectedToVoiceChannel(async voiceChannel =>
            {
                var job = await BilibiliVideoJob.CreateAsync(url);
                await Players.EnqueueAsync(Context.Guild.Id, voiceChannel, job, textChannel);
                await RespondAsync($"Video {job.Name} added to the queue");
            });
        }
        catch (Exception e)
        {
            await textChannel.SendMessageAsync($"Error during command execution: {e.Message}");
        }
    }

    [SlashCommand("local-file", "Play a local file")]
    public async Task QueueFileAsync(string filename)
    {
        if (Context.Interaction is not SocketInteraction {Channel: var textChannel})
        {
            await ReplyAsync("This command can only be invoked in a text channel.");
            return;
        }

        try
        {
            await EnsureConnectedToVoiceChannel(async voiceChannel =>
            {
                var job = LocalFileJob.Create(filename, "/Users/amphineko/Downloads/playable");
                await Players.EnqueueAsync(Context.Guild.Id, voiceChannel, job, textChannel);
                await RespondAsync($"Local file {job.Name} added to the queue");
            });
        }
        catch (Exception e)
        {
            await textChannel.SendMessageAsync($"Error during command execution: {e.Message}");
        }
    }

    [SlashCommand("netease", "Play a Netease Music song from a given id")]
    public async Task QueueNeteaseMusicSongAsync(string id)
    {
        if (Context.Interaction is not SocketInteraction {Channel: var textChannel})
        {
            await ReplyAsync("This command can only be invoked in a text channel.");
            return;
        }

        if (!int.TryParse(id, out var numId))
        {
            await ReplyAsync("Invalid id");
            return;
        }

        try
        {
            await EnsureConnectedToVoiceChannel(async voiceChannel =>
            {
                var job = await NeteaseMusicJobFactory.CreateAsync(numId);
                await Players.EnqueueAsync(Context.Guild.Id, voiceChannel, job, textChannel);
                await RespondAsync($"Netease Music song {job.Name} added to the queue");
            });
        }
        catch (Exception e)
        {
            await textChannel.SendMessageAsync($"Error during command execution: {e.Message}");
        }
    }

    [SlashCommand("pause", "Pause the player")]
    public async Task PauseAsync()
    {
        if (Context.Interaction is not SocketInteraction {Channel: var textChannel})
        {
            await ReplyAsync("This command can only be invoked in a text channel.");
            return;
        }

        try
        {
            await EnsureConnectedToVoiceChannel(async voiceChannel =>
            {
                Players.Pause(Context.Guild.Id, voiceChannel);
                await RespondAsync("Player paused");
            });
        }
        catch (Exception e)
        {
            await textChannel.SendMessageAsync($"Error during command execution: {e.Message}");
        }
    }

    [SlashCommand("resume", "Resume the player")]
    public async Task ResumeAsync()
    {
        if (Context.Interaction is not SocketInteraction {Channel: var textChannel})
        {
            await ReplyAsync("This command can only be invoked in a text channel.");
            return;
        }

        try
        {
            await EnsureConnectedToVoiceChannel(async voiceChannel =>
            {
                Players.Resume(Context.Guild.Id, voiceChannel);
                await RespondAsync("Player resumed");
            });
        }
        catch (Exception e)
        {
            await textChannel.SendMessageAsync($"Error during command execution: {e.Message}");
        }
    }

    [SlashCommand("skip", "Skip the current playing job")]
    public async Task SkipAsync()
    {
        if (Context.Interaction is not SocketInteraction {Channel: var textChannel})
        {
            await ReplyAsync("This command can only be invoked in a text channel.");
            return;
        }

        try
        {
            await EnsureConnectedToVoiceChannel(async voiceChannel =>
            {
                Players.Skip(Context.Guild.Id, voiceChannel);
                await RespondAsync("Player skipped");
            });
        }
        catch (Exception e)
        {
            await textChannel.SendMessageAsync($"Error during command execution: {e.Message}");
        }
    }

    private async Task EnsureConnectedToVoiceChannel(Func<IVoiceChannel, Task> callback)
    {
        if (Context.User is not IGuildUser guildUser)
        {
            await RespondAsync("You must invoke this command in a server.");
            return;
        }

        var voiceChannel = guildUser.VoiceChannel;
        if (voiceChannel is not { })
        {
            await RespondAsync("You must be in a voice channel to invoke this command.");
            return;
        }

        await callback(voiceChannel);
    }
}