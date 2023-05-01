using System.Reflection;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;

namespace ChatBot.Services;

public class StartupService
{
    private readonly DiscordSocketClient _client;
    private readonly IConfigurationRoot _config;
    private readonly CommandService _commandService;
    private readonly IServiceProvider _provider;

    public static readonly CancellationTokenSource Cts = new();

    public StartupService(DiscordSocketClient client, IConfigurationRoot config, CommandService commandService,
        IServiceProvider provider)
    {
        _client = client;
        _config = config;
        _commandService = commandService;
        _provider = provider;
    }

    /// <summary>
    /// Runs on startup
    /// </summary>
    public async Task StartupAsync()
    {
        var botToken = _config["DiscordToken"];
        if (string.IsNullOrWhiteSpace(botToken)) throw new ArgumentNullException("Token is missing");

        await _client.LoginAsync(TokenType.Bot, botToken);
        await _client.StartAsync();

        await _commandService.AddModulesAsync(Assembly.GetExecutingAssembly(), _provider);
    }
}