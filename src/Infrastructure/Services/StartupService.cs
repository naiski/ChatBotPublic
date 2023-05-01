using System.Reflection;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;

namespace ChatBot.Services;

public class StartupService
{
    private readonly IConfigurationRoot _config;
    private readonly IServiceProvider _provider;
    private readonly DiscordSocketClient _client;
    private readonly CommandService _commandService;

    public static readonly CancellationTokenSource Cts = new();

    public StartupService(IConfigurationRoot config,
        IServiceProvider provider,
        DiscordSocketClient client,
        CommandService commandService)
    {
        _config = config;
        _provider = provider;
        _client = client;
        _commandService = commandService;
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