using System.Text.RegularExpressions;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Serilog;

namespace ChatBot.Services;

public class MessageParsingService
{
    private readonly DiscordSocketClient _client;
    private readonly IConfigurationRoot _config;

    private static string[] _nicknames = Array.Empty<string>();
    private static string[] _greetingTriggers = Array.Empty<string>();
    private static string[] _selfieTriggers = Array.Empty<string>();
    private static string[] _photoTriggers = Array.Empty<string>();

    public MessageParsingService(DiscordSocketClient client, IConfigurationRoot config)
    {
        _client = client;
        _config = config;

        // Load trigger strings from config
        _nicknames = (_config.GetSection("Nicknames").Get<string[]>() ?? Array.Empty<string>())
            // Add name to nicknames
            .Append(_config["Name"] ?? string.Empty).ToArray();
        _greetingTriggers = _config.GetSection("GreetingTriggers").Get<string[]>() ?? Array.Empty<string>();
        _selfieTriggers = _config.GetSection("SelfieTriggers").Get<string[]>() ?? Array.Empty<string>();
        _photoTriggers = _config.GetSection("PhotoTriggers").Get<string[]>() ?? Array.Empty<string>();
        // Sort by length (longest strings first) so that substrings are checked last
        Array.Sort(_nicknames, (x, y) => y.Length.CompareTo(x.Length));
        Array.Sort(_greetingTriggers, (x, y) => y.Length.CompareTo(x.Length));
        Array.Sort(_selfieTriggers, (x, y) => y.Length.CompareTo(x.Length));
        Array.Sort(_photoTriggers, (x, y) => y.Length.CompareTo(x.Length));
    }

    /// <summary>
    /// Class to represent parsed message data
    /// </summary>
    public class ParsedMessage
    {
        public IUserMessage OriginalMessage;
        public int ArgPos;
        public readonly bool ShouldRespond;
        public KeyValuePair<string, string> Author;
        public KeyValuePair<string, string> Channel;
        public readonly string Content = string.Empty;
        public readonly string Command = string.Empty;
        public bool HasImagePrompt => HasSelfiePrompt || HasPhotoPrompt;
        public bool HasSelfiePrompt => !string.IsNullOrEmpty(SelfiePrompt);
        public bool HasPhotoPrompt => !string.IsNullOrEmpty(PhotoPrompt);

        // Parses command for selfie trigger strings & sets prompt to the rest of the command
        public string SelfiePrompt
        {
            get
            {
                foreach (var prompt in _selfieTriggers)
                {
                    if (Command.StartsWith(prompt, StringComparison.OrdinalIgnoreCase))
                    {
                        // Hack to post generic selfie when no further input is given i.e. "take a selfie"
                        if (prompt.Equals(Command)) return " ";
                        return Command.Remove(0, prompt.Length).Trim(',').Trim();
                    }
                }

                // If we get this far it's not a selfie request
                return string.Empty;
            }
        }

        // Parses command for photo trigger strings & sets prompt to the rest of the command
        public string PhotoPrompt
        {
            get
            {
                foreach (var prompt in _photoTriggers)
                {
                    if (Command.StartsWith(prompt, StringComparison.OrdinalIgnoreCase))
                    {
                        return Command.Remove(0, prompt.Length).Trim(',').Trim();
                    }
                }

                // If we get this far it's not a photo request
                return string.Empty;
            }
        }

        public ParsedMessage(IUserMessage msg, int argPos, bool shouldRespond, string content, string command)
        {
            // Adjust argPos to whitespace
            while (argPos < msg.Content.Length && char.IsWhiteSpace(msg.Content[argPos])) argPos++;
            OriginalMessage = msg;
            ArgPos = argPos;
            ShouldRespond = shouldRespond;
            Author = new KeyValuePair<string, string>(msg.Author.Id.ToString(), SanitizeString(msg.Author.Username));
            Channel = new KeyValuePair<string, string>(msg.Channel.Id.ToString(), SanitizeString(msg.Channel.Name));
            Content = content;
            Command = command;
        }
    }

    /// <summary>
    /// Filters out stuff that we don't want in the chat log
    /// </summary>
    /// <param name="s">The string to be filtered</param>
    /// <returns>The filtered string</returns>
    public static string SanitizeString(string s)
    {
        return Regex.Replace(s, @"[^a-zA-Z0-9""'!@#$%&=\s\n\.\+\*\(\)\[\]\$]", string.Empty);
    }

    /// <summary>
    /// Logs parsed message data
    /// </summary>
    /// <param name="msg">The parsed message to log</param>
    public static void LogParsedMessageAsync(ParsedMessage msg)
    {
        var json = JsonConvert.SerializeObject(
            new
            {
                msg.ShouldRespond,
                Author = msg.Author.Value,
                Channel = msg.Channel.Value,
                msg.Content,
                msg.Command,
                msg.SelfiePrompt,
                msg.PhotoPrompt
            }, Formatting.Indented, new StringEnumConverter());
        var logMsg = $"Parsed message: {json}";
        Log.Debug("[{Source}] {Message}", "MessageParsingService", logMsg);
    }

    /// <summary>
    /// Parses a message
    /// </summary>
    /// <param name="msg">The message to parse</param>
    /// <returns></returns>
    public ParsedMessage ParseMessageAsync(IUserMessage msg)
    {
        // Ref variable for parsing prefixes
        var argPos = 0;

        // Handle direct mentions
        if (msg.HasMentionPrefix(_client.CurrentUser, ref argPos) ||
            msg.MentionedUserIds.Contains(_client.CurrentUser.Id))
        {
            return new ParsedMessage(
                msg,
                argPos,
                shouldRespond: true,
                content: msg.Content.Replace($"<@{_client.CurrentUser.Id}>", _config["Name"]).Trim(),
                command: msg.Content.Replace($"<@{_client.CurrentUser.Id}>", "").Trim());
        }

        // Handle "{greeting} {nickname}"
        if ((from greeting in _greetingTriggers
                from nickname in _nicknames
                where msg.HasStringPrefix($"{greeting} {nickname}", ref argPos, StringComparison.OrdinalIgnoreCase)
                select greeting).Any())
        {
            return new ParsedMessage(
                msg,
                argPos,
                shouldRespond: true,
                content: msg.Content,
                command: msg.Content[argPos..].Trim(',').Trim());
        }

        // Handle "{nickname}"
        if (_nicknames.Any(
                nickname => msg.HasStringPrefix($"{nickname}", ref argPos, StringComparison.OrdinalIgnoreCase)))
        {
            return new ParsedMessage(
                msg,
                argPos,
                shouldRespond: true,
                content: msg.Content,
                command: msg.Content[argPos..].Trim(',').Trim());
        }

        // If we get this far the message isn't for us
        return new ParsedMessage(
            msg,
            argPos,
            shouldRespond: false, // Don't respond to this message
            content: msg.Content,
            command: string.Empty);
    }
}