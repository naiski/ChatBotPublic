using ChatBot.Services;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Serilog;
using SixLabors.ImageSharp.Formats.Png;

namespace Services.Events;

public class MessageReceivedEvent
{
    private readonly DiscordSocketClient _client;
    private readonly IConfigurationRoot _config;
    private readonly TextGenerationService _textGenerationService;
    private readonly StableDiffusionService _stableDiffusionService;
    private readonly MessageParsingService _messageParsingService;
    private readonly CommandService _commandService;
    private readonly IServiceProvider _provider;

    private static string[] _allowedChannels = Array.Empty<string>();

    public MessageReceivedEvent(DiscordSocketClient client, IConfigurationRoot config,
        TextGenerationService textGenerationService,
        StableDiffusionService stableDiffusionService, MessageParsingService messageParsingService)
    {
        _client = client;
        _config = config;
        _textGenerationService = textGenerationService;
        _stableDiffusionService = stableDiffusionService;
        _messageParsingService = messageParsingService;

        _client.MessageReceived += MessageReceivedEventHandler;

        _allowedChannels = _config.GetSection("AllowedChannels").Get<string[]>() ?? Array.Empty<string>();
    }

    /// <summary>
    /// Fires when a message is received
    /// </summary>
    /// <param name="arg">The message that was received</param>
    private async Task MessageReceivedEventHandler(SocketMessage arg)
    {
        var msg = arg as SocketUserMessage;
        if (null == msg) return;

        // Ignore messages from self
        if (msg.Author.Id == _client.CurrentUser.Id) return;

        // Get message context for replying
        var context = new SocketCommandContext(_client, msg);

        // If channel is set, ignore messages not in specified channel
        if (_allowedChannels.Length > 0)
            if (!_allowedChannels.Contains(msg.Channel.Id.ToString()))
                return;

        // Parse the incoming message
        var parsedMessage = _messageParsingService.ParseMessageAsync(msg);
        // Log the message
        MessageParsingService.LogParsedMessageAsync(parsedMessage);
        if (parsedMessage.ShouldRespond)
        {
            // Handle commands from owner
            if (msg.Author.Id.ToString().Equals(_config["Owner"]))
            {
                Log.Information("[{Source}] {Message}",
                    "MessageHandler",
                    "Received command from owner");
                var result = await _commandService.ExecuteAsync(
                    context: context,
                    argPos: parsedMessage.ArgPos,
                    services: _provider);
                if (result.IsSuccess)
                {
                    Log.Information("[{Source}] {Message}",
                        "MessageHandler",
                        "Executed command from owner");
                    return;
                }
            }

            // Start typing until we exit this code block
            using var typing = context.Channel.EnterTypingState();

            // Check if this is an image generation request
            if (parsedMessage.HasImagePrompt)
            {
                try
                {
                    // Generate an image
                    var image = await _stableDiffusionService.GetImageAsync(parsedMessage);
                    // Save the image to a temp file
                    await (image ?? throw new InvalidOperationException()).SaveAsync("temp.png", new PngEncoder());
                    // Post the image
                    await context.Channel.SendFileAsync("temp.png");
                    // Delete the temp file
                    File.Delete("temp.png");
                }
                catch (Exception e)
                {
                    Log.Error("[{Source}] {Message}", "MessageHandler",
                        $"Error saving image: \"{e.Message}\"\n{e.StackTrace}");
                }
            }

            // Generate a text reply
            var reply = await _textGenerationService.GetReplyAsync(parsedMessage);
            // Post the reply, unless it's empty
            if (!string.IsNullOrWhiteSpace(reply)) await msg.ReplyAsync(reply);
        }
    }
}