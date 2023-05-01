using Flurl;
using Flurl.Http;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using Serilog;

namespace ChatBot.Services;

public class StableDiffusionService
{
    private readonly IConfigurationRoot _config;

    public StableDiffusionService(IConfigurationRoot config)
    {
        _config = config;
    }

    /// <summary>
    /// Generates an image based on the parsed message
    /// </summary>
    /// <param name="msg">The parsed message to generate the image fom</param>
    /// <returns>A newly generated image</returns>
    public async Task<Image?> GetImageAsync(MessageParsingService.ParsedMessage msg)
    {
        // Build an image prompt from the config + message
        var prompt = msg.HasSelfiePrompt ? _config["SelfiePrompt"] + ", " + msg.SelfiePrompt : msg.PhotoPrompt;
        var negative_prompt = msg.HasSelfiePrompt ? _config["ImageNegativePrompt"] : string.Empty;
        try
        {
            Log.Debug("[{Source}] {Message}", "StableDiffusionService",
                $"Sending image generation request to server with prompt:\n<PROMPT BEGIN>\n{prompt}\n<PROMPT END>\n" +
                $"<NEGATIVE PROMPT BEGIN>\n{negative_prompt}\n<NEGATIVE PROMPT END>");
            var response = await _config["StableDiffusionServer"]
                .AppendPathSegments("sdapi", "v1", "txt2img")
                .PostJsonAsync(new
                {
                    prompt,
                    negative_prompt
                });
            // Get image data from response as a base64 string
            var jsonResponse = JObject.Parse(await response.GetStringAsync());
            var images = jsonResponse["images"]?.ToObject<JArray>();
            var imageData = (images ?? throw new InvalidOperationException()).First().ToString();
            var commaIndex = imageData.IndexOf(',') + 1;
            var base64 = imageData[commaIndex..];
            // Decode the base64 string to an image
            using var imageStream = new MemoryStream(Convert.FromBase64String(base64));
            return await Image.LoadAsync(imageStream);
        }
        catch (Exception e)
        {
            Log.Error("[{Source}] {Message}", "StableDiffusionService",
                $"Error generating image: \"{e.Message}\"\n{e.StackTrace}");
            return null;
        }
    }
}