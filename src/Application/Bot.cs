using ChatBot.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Services;

namespace Application;

internal static class Bot
{
    /// <summary>
    /// Main routine
    /// </summary>
    public static async Task Main()
    {
        // Create logger
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .CreateLogger();

        // Build config
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("config.json")
            .Build();

        // Build services
        await new ServiceCollection()
            .AddSingleton(config)
            .AddInfrastructureServices()
            .BuildServiceProvider()
            .StartInfrastructureServices();

        // Cancel on CTRL+C
        Console.CancelKeyPress += (_, e) =>
        {
            StartupService.Cts.Cancel();
            e.Cancel = true;
        };

        // Spin until cancelled
        try
        {
            await Task.Delay(-1, StartupService.Cts.Token);
        }
        catch (TaskCanceledException)
        {
            Log.Warning("[{Source}] {Message}",
                "Bot",
                "Shutting down...");
        }
    }
}