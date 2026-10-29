using System.Net.Http.Json;
using System.Text.Json.Serialization;

using Generellem.DependencyInjection;
using Generellem.Llm;
using Generellem.Services;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpenAI.Chat;

namespace Generellem.Document;

/// <summary>
/// Embedded image text plus an optional vision-model description.
/// </summary>
public sealed class VisionImageTextExtractor(
    IOptions<GenerellemOptions> options,
    IGenerellemHttpClientFactory httpClientFactory,
    ILogger<VisionImageTextExtractor> logger,
    ILlmClientFactory? llmClientFactory = null) : IImageTextExtractor
{
    public const string Prompt =
        "Transcribe every visible word in this image. If the image has no text, describe it in one paragraph.";

    readonly GenerellemOptions options = options.Value;

    public async Task<string> ExtractAsync(Stream image, string fileName, CancellationToken cancellationToken)
    {
        using MemoryStream copy = new();
        await image.CopyToAsync(copy, cancellationToken);
        byte[] bytes = copy.ToArray();
        string embedded = EmbeddedImageText.Read(bytes, fileName);

        if (!options.EnableImageVision)
            return embedded;

        try
        {
            string? vision = options.Profile == GenerellemProfile.Azure
                ? await DescribeWithAzureAsync(bytes, fileName, cancellationToken)
                : await DescribeWithOllamaAsync(bytes, cancellationToken);

            return EmbeddedImageText.Merge(embedded, vision);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Image vision failed for {FileName}. Embedded text was kept.", fileName);
            return embedded;
        }
    }

    async Task<string?> DescribeWithOllamaAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        string model = string.IsNullOrWhiteSpace(options.VisionModel)
            ? options.OllamaChatModel
            : options.VisionModel;

        using HttpClient client = httpClientFactory.Create();
        client.BaseAddress = new Uri(options.OllamaEndpoint.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromMinutes(10);

        OllamaVisionRequest body = new()
        {
            Model = model,
            Stream = false,
            Messages =
            [
                new OllamaVisionMessage
                {
                    Role = "user",
                    Content = Prompt,
                    Images = [Convert.ToBase64String(bytes)]
                }
            ]
        };

        using HttpResponseMessage response = await client.PostAsJsonAsync("api/chat", body, cancellationToken);
        response.EnsureSuccessStatusCode();
        OllamaVisionResponse? payload = await response.Content.ReadFromJsonAsync<OllamaVisionResponse>(cancellationToken);
        return payload?.Message?.Content;
    }

    async Task<string?> DescribeWithAzureAsync(byte[] bytes, string fileName, CancellationToken cancellationToken)
    {
        if (llmClientFactory is null)
            throw new InvalidOperationException("Azure image vision requires ILlmClientFactory.");

        string mediaType = MediaType(fileName);
        UserChatMessage message = new(
            ChatMessageContentPart.CreateTextPart(Prompt),
            ChatMessageContentPart.CreateImagePart(BinaryData.FromBytes(bytes), mediaType));

        ChatClient chatClient = llmClientFactory.CreateChatClient();
        ChatCompletion completion = await chatClient.CompleteChatAsync([message], cancellationToken: cancellationToken);
        return completion.Content.Count > 0 ? completion.Content[0].Text : string.Empty;
    }

    public static string MediaType(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            _ => "image/png"
        };

    sealed class OllamaVisionRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public List<OllamaVisionMessage> Messages { get; set; } = [];

        [JsonPropertyName("stream")]
        public bool Stream { get; set; }
    }

    sealed class OllamaVisionMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("images")]
        public List<string> Images { get; set; } = [];
    }

    sealed class OllamaVisionResponse
    {
        [JsonPropertyName("message")]
        public OllamaVisionMessage? Message { get; set; }
    }
}
