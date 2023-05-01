using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Serilog;
using Serilog.Events;

namespace ChatBot.Services;

public class LogService
{
    private readonly DiscordSocketClient _client;
    private readonly CommandService _commands;

    public LogService(DiscordSocketClient client, CommandService commands)
    {
        _client = client;
        _commands = commands;

        _client.Log += LogAsync;
        _commands.Log += LogAsync;
    }

    /// <summary>
    /// Logs Discord log messages with Serilog
    /// </summary>
    /// <param name="message">The Discord log message to be logged</param>
    private static async Task LogAsync(LogMessage message)
    {
        var severity = message.Severity switch
        {
            LogSeverity.Critical => LogEventLevel.Fatal,
            LogSeverity.Error => LogEventLevel.Error,
            LogSeverity.Warning => LogEventLevel.Warning,
            LogSeverity.Info => LogEventLevel.Information,
            LogSeverity.Verbose => LogEventLevel.Verbose,
            LogSeverity.Debug => LogEventLevel.Debug,
            _ => LogEventLevel.Information
        };
        Log.Write(severity, message.Exception, "[{Source}] {Message}", message.Source, message.Message);
        await Task.CompletedTask;
    }
}