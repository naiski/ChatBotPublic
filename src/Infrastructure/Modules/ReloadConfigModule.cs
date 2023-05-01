using Microsoft.Extensions.Configuration;

namespace ChatBot.Modules;

using Discord.Commands;

public class ReloadConfigModule : ModuleBase<SocketCommandContext>
{
    private readonly IConfigurationRoot _config;

    public ReloadConfigModule(IConfigurationRoot config)
    {
        _config = config;
    }

    [Command("reload your configuration")]
    [Summary("Reloads config.json")]
    public async Task ResetChatAsync()
    {
        await ReplyAsync("Okay, I'm reloading my configuration file!");
        _config.Reload();
    }
}