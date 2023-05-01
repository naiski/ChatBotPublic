using ChatBot.Services;

namespace ChatBot.Modules;

using Discord.Commands;

public class ResetChatModule : ModuleBase<SocketCommandContext>
{
    private readonly TextGenerationService _textGenerationService;

    public ResetChatModule(TextGenerationService textGenerationService)
    {
        _textGenerationService = textGenerationService;
    }

    [Command("wipe your memory")]
    [Summary("Clears chat history")]
    public async Task ResetChatAsync()
    {
        await ReplyAsync("Okay, wiping my memory!");
        _textGenerationService.ResetChatHistory();
    }
}