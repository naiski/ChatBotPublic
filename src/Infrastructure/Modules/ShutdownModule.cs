using ChatBot.Services;

namespace ChatBot.Modules;

using Discord.Commands;

public class ShutdownModule : ModuleBase<SocketCommandContext>
{
    private readonly StartupService _startupService;

    public ShutdownModule(StartupService startupService)
    {
        _startupService = startupService;
    }

    [Command("go to sleep")]
    [Summary("Shuts down the bot")]
    public async Task ShutdownAsync()
    {
        await ReplyAsync("Okay, Goodnight!");
        StartupService.Cts.Cancel();
    }
}