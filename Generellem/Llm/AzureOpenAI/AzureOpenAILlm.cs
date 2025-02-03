using Azure;

using Generellem.Services;

using Microsoft.Extensions.Logging;

using OpenAI.Chat;

using Polly;
using Polly.Retry;

namespace Generellem.Llm.AzureOpenAI;

public class AzureOpenAILlm(ILlmClientFactory llmClientFact, ILogger<AzureOpenAILlm> logger) : ILlm
{
    readonly ILogger<AzureOpenAILlm> logger = logger;

    public ResiliencePipeline Pipeline { get; set; } = 
        new ResiliencePipelineBuilder()
            .AddRetry(
                new RetryStrategyOptions
                {
                    ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,  // Adds a random factor to the delay
                    MaxRetryAttempts = 10,
                    Delay = TimeSpan.FromSeconds(3),
                })
            .Build();

    public virtual async Task<TResponse> PromptAsync<TResponse>(IChatRequest? chatRequest, CancellationToken cancellationToken)
        where TResponse : IChatResponse
    {
        AzureOpenAIChatRequest? request = chatRequest as AzureOpenAIChatRequest;
        ArgumentNullException.ThrowIfNull(request, nameof(request));
        ArgumentNullException.ThrowIfNull(request.Messages, nameof(request.Messages));

        try
        {
            ChatClient chatClient = llmClientFact.CreateChatClient();
            ChatCompletion chatCompletionsResponse =
                await Pipeline.ExecuteAsync<ChatCompletion>(
                    async token => await chatClient.CompleteChatAsync(request.Messages, request.Options, token),
                    cancellationToken);

            IChatResponse chatResponse = new AzureOpenAIChatResponse(chatCompletionsResponse);

            return (TResponse)chatResponse;
        }
        catch (RequestFailedException rfEx)
        {
            logger.LogError(GenerellemLogEvents.AuthorizationFailure, rfEx, "Please check credentials and exception details for more info.");
            throw;
        }
    }
}
