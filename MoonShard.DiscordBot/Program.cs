using System.Diagnostics;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MoonShard.DiscordBot;
using MoonShard.DiscordBot.Commands;
using MoonShard.DiscordBot.ExternalServices.NeteaseMusic;
using MoonShard.DiscordBot.Services.AudioClients;
using MoonShard.DiscordBot.Services.AudioPlayers;

var config = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .Build();

var host = new HostBuilder()
    .ConfigureAppConfiguration(builder => { builder.AddEnvironmentVariables(); })
    .ConfigureLogging(builder =>
    {
        if (Debugger.IsAttached) builder.SetMinimumLevel(LogLevel.Debug);

        builder.AddConsole();
    })
    .ConfigureServices(services =>
    {
        services.AddSingleton(sp => sp.GetRequiredService<IConfiguration>().Get<ApplicationConfiguration>());

        services.AddSingleton(sp =>
        {
            var client = new DiscordSocketClient(new DiscordSocketConfig
            {
                DefaultRetryMode = RetryMode.AlwaysRetry,
                LogLevel = Debugger.IsAttached ? LogSeverity.Debug : LogSeverity.Info
            });

            var logger = sp.GetRequiredService<ILogger<DiscordSocketClient>>();
            client.Log += message =>
            {
                var severity = message.Severity switch
                {
                    LogSeverity.Critical => LogLevel.Critical,
                    LogSeverity.Error => LogLevel.Error,
                    LogSeverity.Warning => LogLevel.Warning,
                    LogSeverity.Info => LogLevel.Information,
                    LogSeverity.Verbose => LogLevel.Debug,
                    LogSeverity.Debug => LogLevel.Debug,
                    _ => LogLevel.Information
                };
                logger.Log(severity, message.Exception, "{message}", message.Message);

                return Task.CompletedTask;
            };

            return client;
        });

        services.AddSingleton(sp =>
        {
            var interaction = new InteractionService(sp.GetRequiredService<DiscordSocketClient>());
            interaction.AddModuleAsync<AudioPlayerCommands>(sp);

            return interaction;
        });

        services.AddSingleton<AudioClientRepository>();
        services.AddSingleton<AudioPlayerRepository>();

        services.AddHostedService<Client>();

        services.AddSingleton(sp =>
        {
            var endpoint = sp.GetRequiredService<ApplicationConfiguration>().NeteaseMusicApiEndpoint;
            return new NeteaseMusicJobFactory(!string.IsNullOrWhiteSpace(endpoint) ? endpoint : null);
        });
    })
    .Build();

await host.RunAsync();