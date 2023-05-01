namespace ChatBot.Modules;

using Discord.Commands;

public class EchoModule : ModuleBase<SocketCommandContext>
{
    [Command("say")]
    [Summary("Echoes a message")]
    public async Task EchoAsync([Remainder] [Summary("The text to echo")] string echo)
    {
        await ReplyAsync(echo);
    }
}