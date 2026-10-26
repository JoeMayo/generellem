using System.Net.Http.Json;
using System.Text.Json.Serialization;

using Generellem.DependencyInjection;
using Generellem.Llm.AzureOpenAI;
using Generellem.Services;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpenAI.Chat;

using Polly;

namespace Generellem.Llm.Ollama;

/// <summary>
/// Chat completions via local Ollama HTTP API.
/// </summary>
public class OllamaLlm(
    IGenerellemHttpClientFactory httpClientFactory,
    IOptions<GenerellemOptions> options,
    ILogger<OllamaLlm> logger)
    : ILlm
{
    readonly GenerellemOptions options = options.Value;

    public ResiliencePipeline Pipeline { get; set; } =
        new ResiliencePipelineBuilder()
            .AddRetry(new()
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                MaxRetryAttempts = 5,
                Delay = TimeSpan.FromSeconds(2)
            })
            .Build();

    public async Task<TResponse> PromptAsync<TResponse>(IChatRequest request, CancellationToken cancellationToken)
        where TResponse : IChatResponse
    {
        if (request is not AzureOpenAIChatRequest chatRequest)
            throw new ArgumentException($"OllamaLlm expects {nameof(AzureOpenAIChatRequest)}.", nameof(request));

        ArgumentNullException.ThrowIfNull(chatRequest.Messages);

        try
        {
            List<OllamaMessage> messages = [];
            foreach (ChatMessage message in chatRequest.Messages)
            {
                string role = message switch
                {
                    SystemChatMessage => "system",
                    AssistantChatMessage => "assistant",
                    UserChatMessage => "user",
                    _ => "user"
                };

                messages.Add(new OllamaMessage
                {
                    Role = role,
                    Content = AzureOpenAIChatRequest.GetRequestContent(message)
                });
            }

            using HttpClient client = httpClientFactory.Create();
            client.BaseAddress = new Uri(options.OllamaEndpoint.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromMinutes(10);

            OllamaChatRequest body = new()
            {
                Model = options.OllamaChatModel,
                Messages = messages,
                Stream = false,
                Options = new OllamaRequestOptions
                {
                    Temperature = chatRequest.Options?.Temperature ?? options.Temperature
                }
            };

            OllamaChatResponse? response = await Pipeline.ExecuteAsync(
                async token =>
                {
                    using HttpResponseMessage httpResponse = await client.PostAsJsonAsync("api/chat", body, token);
                    httpResponse.EnsureSuccessStatusCode();
                    return await httpResponse.Content.ReadFromJsonAsync<OllamaChatResponse>(cancellationToken: token);
                },
                cancellationToken);

            string text = response?.Message?.Content ?? string.Empty;
            IChatResponse chatResponse = new AzureOpenAIChatResponse(text);
            return (TResponse)chatResponse;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ollama chat failed. Is Ollama running at {Endpoint} with model {Model}?",
                options.OllamaEndpoint, options.OllamaChatModel);
            throw;
        }
    }

    sealed class OllamaChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public List<OllamaMessage> Messages { get; set; } = [];

        [JsonPropertyName("stream")]
        public bool Stream { get; set; }

        [JsonPropertyName("options")]
        public OllamaRequestOptions Options { get; set; } = new();
    }

    sealed class OllamaRequestOptions
    {
        [JsonPropertyName("temperature")]
        public float Temperature { get; set; }
    }

    sealed class OllamaMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    sealed class OllamaChatResponse
    {
        [JsonPropertyName("message")]
        public OllamaMessage? Message { get; set; }
    }
}
