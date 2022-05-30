using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MoonShard.DiscordBot;

public class Client : IHostedService
{
    private readonly DiscordSocketClient _client;
    private readonly ApplicationConfiguration _config;
    private readonly InteractionService _interactionService;
    private readonly ILogger<Client> _logger;
    private readonly IServiceProvider _services;

    public Client(
        ApplicationConfiguration config,
        DiscordSocketClient client,
        ILogger<Client> logger,
        IServiceProvider services,
        InteractionService interactionService)
    {
        _client = client;
        _config = config;
        _logger = logger;
        _interactionService = interactionService;
        _services = services;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _client.GuildAvailable += guild =>
        {
            Task.Run(async () => { await _interactionService.RegisterCommandsToGuildAsync(guild.Id); },
                cancellationToken);
            return Task.CompletedTask;
        };

        _client.InteractionCreated += async interaction =>
        {
            var context = new InteractionContext(_client, interaction);
            try
            {
                var result = await _interactionService.ExecuteCommandAsync(context, _services);
                if (!result.IsSuccess) await interaction.RespondAsync($"Internal error: {result.ErrorReason}");
            }
            catch (Exception e)
            {
                await interaction.DeleteOriginalResponseAsync();
                await interaction.RespondAsync($"Internal error with exception: {e.Message}");
            }
        };

        _logger.LogDebug("Attempting to log-in");
        await _client.LoginAsync(TokenType.Bot, _config.Token);
        await _client.StartAsync();
        _logger.LogInformation("Logged-in");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _client.StopAsync();
    }
}