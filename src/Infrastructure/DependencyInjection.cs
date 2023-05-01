using ChatBot.Services;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Services.Events;

namespace Services;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent,
            LogLevel = LogSeverity.Debug,
            MessageCacheSize = 50
        }));
        services.AddSingleton(new CommandService(new CommandServiceConfig
        {
            DefaultRunMode = RunMode.Async
        }));
        services.AddSingleton<StartupService>();
        services.AddSingleton<LogService>();
        services.AddSingleton<TextGenerationService>();
        services.AddSingleton<MessageParsingService>();
        services.AddSingleton<StableDiffusionService>();
        // Events
        services.AddTransient<MessageReceivedEvent>();

        return services;
    }

    public static async Task StartInfrastructureServices(this IServiceProvider provider)
    {
        // Start the logging service
        provider.GetRequiredService<LogService>();
        // Start and execute the startup service
        await provider.GetRequiredService<StartupService>().StartupAsync();
        // Start event handlers
        provider.GetRequiredService<MessageReceivedEvent>();
    }
}