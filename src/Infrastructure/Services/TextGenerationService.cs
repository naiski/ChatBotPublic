using System.Text;
using System.Text.RegularExpressions;
using Flurl;
using Flurl.Http;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Serilog;

namespace ChatBot.Services;

public class TextGenerationService
{
    private readonly IConfigurationRoot _config;

    // In-memory chat history is stored as a string in a dictionary, channel IDs serve as keys
    private readonly Dictionary<string, string> _chatHistory;

    // Static access for the bot's configured name
    private static string _botname = string.Empty;

    // Static access for the bot's configured selfie responses e.g. "Here's a photo of me"
    private static string[] _selfieResponses = Array.Empty<string>();

    // Static access for the bot's configured photo responses e.g. "Here's a photo of a"
    private static string[] _photoResponses = Array.Empty<string>();

    // Static access for the bot's configured selfie responses e.g. "Sorry, I'm having trouble thinking right now..."
    private static string[] _errorResponses = Array.Empty<string>();

    // Set up convenience properties to grab random responses
    private static readonly Random _random = new();
    private static string RandomErrorResponse => _errorResponses[_random.Next(_errorResponses.Length)];
    private static string RandomSelfieResponse => _selfieResponses[_random.Next(_selfieResponses.Length)];
    private static string RandomPhotoResponse => _photoResponses[_random.Next(_photoResponses.Length)];

    public TextGenerationService(IConfigurationRoot config)
    {
        _config = config;

        _botname = _config["Name"] ?? string.Empty;
        _chatHistory = new Dictionary<string, string>();
        // Load responses from config into static arrays
        _errorResponses = _config.GetSection("ErrorResponses").Get<string[]>() ?? Array.Empty<string>();
        _selfieResponses = _config.GetSection("SelfieResponses").Get<string[]>() ?? Array.Empty<string>();
        _photoResponses = _config.GetSection("PhotoResponses").Get<string[]>() ?? Array.Empty<string>();
    }

    /// <summary>
    /// Class to represent server response json result
    /// </summary>
    private class TextGenerationResult
    {
        [JsonProperty("text")] public string? Text { get; set; }
    }

    /// <summary>
    /// Class to represent server response json
    /// </summary>
    private class TextGenerationResponse
    {
        [JsonProperty("results")] public IList<TextGenerationResult>? Results { get; set; }
    }

    /// <summary>
    /// Wipes all chat history
    /// </summary>
    public void ResetChatHistory()
    {
        _chatHistory.Clear();
    }

    /// <summary>
    /// Format messages for our in-memory chat log
    /// </summary>
    /// <param name="author">Author of the message</param>
    /// <param name="content">Content of the message</param>
    /// <returns>The formatted chat log message</returns>
    private static string BuildChatLogMessage(string author, string content)
    {
        return $"({author}): {content}";
    }

    /// <summary>
    /// Gets a string used to prime replies that come after an image e.g. "Here's a photo of me "
    /// </summary>
    /// <param name="msg">The received message</param>
    /// <returns>The string described in the summary</returns>
    private static string GetImageReplySeed(MessageParsingService.ParsedMessage msg)
    {
        if (msg.HasSelfiePrompt)
            return RandomSelfieResponse + " ";
        if (msg.HasImagePrompt)
            return RandomPhotoResponse + " ";
        return string.Empty;
    }

    /// <summary>
    /// Gets a string that pretends to have posted an image e.g. "Bot: (Posts a selfie)"
    /// </summary>
    /// <param name="msg">The received message</param>
    /// <returns>The string described in the summary</returns>
    private static string GetImagePostMessage(MessageParsingService.ParsedMessage msg)
    {
        if (msg.HasSelfiePrompt)
            return string.IsNullOrWhiteSpace(msg.SelfiePrompt)
                ? BuildChatLogMessage(_botname, "(Posts a selfie)")
                : BuildChatLogMessage(_botname, $"(Posts a selfie {msg.SelfiePrompt})");
        if (msg.HasImagePrompt)
            return BuildChatLogMessage(_botname, $"(Posts a picture of a {msg.PhotoPrompt})");
        return string.Empty;
    }

    public static string SanitizeReply(string reply)
    {
        // Sometimes third-party replies slip through stopping_strings, so strip them off if we find them
        reply = Regex.Replace(reply, @"(^.*)(\n\(.*\r?\n?)+$", "$1");
        return reply;
    }

    /// <summary>
    /// Generates a reply to a parsed message (kind of a mess)
    /// </summary>
    /// <param name="msg">The parsed message to reply to</param>
    /// <returns>A reply to the parsed message</returns>
    public async Task<string?> GetReplyAsync(MessageParsingService.ParsedMessage msg)
    {
        // Initialize chat history for new channels
        _chatHistory.TryAdd(msg.Channel.Key, string.Empty);
        // Append the message we just received
        _chatHistory[msg.Channel.Key] += BuildChatLogMessage(msg.Author.Value, msg.Content);
        // For replies succeeding an image, insert a message pretending we just posted the image e.g. "(Posts a selfie)"
        var imagePostMessage = GetImagePostMessage(msg);
        // For replies succeeding an image post, seed replies with a short message e.g. "Here's a photo of me "
        var imageReplySeed = GetImageReplySeed(msg);
        // Build the prompt
        var sb = new StringBuilder();
        sb.Append($"Below is a conversation between {msg.Author.Value} and {_botname}. {_config["Persona"]}\r\n");
        sb.Append("### INSTRUCTION: Write the next response in the conversation.\r\n");
        sb.Append(_chatHistory[msg.Channel.Key] + "\r\n");
        if (msg.HasImagePrompt) sb.Append(imagePostMessage + "\r\n");
        sb.Append("### RESPONSE:\r\n");
        sb.Append(BuildChatLogMessage(_botname, ""));
        if (msg.HasImagePrompt) sb.Append(imageReplySeed);
        var prompt = sb.ToString();
        var reply = string.Empty;
        Log.Debug("[{Source}] {Message}", "TextGenerationService",
            $"Sending text generation request to server with prompt:\n[PROMPT BEGIN]\n{prompt}\n[PROMPT END]");
        try
        {
            var response = await _config["TextGenerationServer"]
                .AppendPathSegments("api", "v1", "generate")
                .PostJsonAsync(new
                {
                    prompt,
                    max_new_tokens = 200,
                    do_sample = false,
                    temperature = 0.85,
                    top_p = 0.9,
                    typical_p = 1,
                    repetition_penalty = 1.1,
                    encoder_repetition_penalty = 1,
                    top_k = 40,
                    num_beams = 1,
                    penalty_alpha = 0,
                    min_length = 0,
                    length_penalty = 1,
                    no_repeat_ngram_size = 0,
                    early_stopping = true,
                    stopping_strings = new[] { "\n#", "\n##", "\n###", "(" },
                    seed = -1,
                    add_bos_token = true
                });
            var result = await response.GetJsonAsync<TextGenerationResponse>();
            reply = (result.Results ?? throw new InvalidOperationException()).First().Text ??
                    throw new InvalidOperationException();
            Log.Debug("[{Source}] {Message}", "TextGenerationService",
                $"Got text generation reply:\n<REPLY BEGIN>\n{reply}\n<REPLY END>");
            reply = SanitizeReply(reply);
            // On success, update chat history (spaghetti intensifies)
            if (msg.HasImagePrompt)
            {
                // For replies succeeding an image post, include any extra prompting we did
                _chatHistory[msg.Channel.Key] += "\r\n" + imagePostMessage + "\r\n" +
                                                 BuildChatLogMessage(_botname, "");
                // Also need to concatenate the reply seed with the generated reply
                reply = imageReplySeed + reply;
            }
            else
            {
                // Otherwise just include the empty response message e.g. "Bot: "
                _chatHistory[msg.Channel.Key] += "\r\n" + BuildChatLogMessage(_botname, "");
            }
        }
        catch (Exception e)
        {
            Log.Error("[{Source}] {Message}", "TextGenerationService",
                $"Error generating response: \"{e.Message}\"\n{e.StackTrace}");
            // Grab a random error response instead e.g. "Sorry, I'm having trouble thinking right now..."
            reply = RandomErrorResponse;
            // On error do NOT update chat history with any image prompting, just include the empty response
            _chatHistory[msg.Channel.Key] += "\r\n" + BuildChatLogMessage(_botname, "");
        }

        // Add the generated reply to chat history
        _chatHistory[msg.Channel.Key] += reply + "\r\n";

        // TODO: remove this
        // @ the user when responding to a bot to maximize chances of creating a feedback loop
        if (msg.OriginalMessage.Author.IsBot) reply = $"<@{msg.OriginalMessage.Author.Id}> " + reply;

        return reply;
    }
}