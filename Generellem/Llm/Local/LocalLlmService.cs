using System.Runtime.CompilerServices;

using Generellem.Llm.AzureOpenAI;

using OpenAI.Chat;

namespace Generellem.Llm.Local;

/// <summary>
/// Offline LLM that returns extractive answers from retrieved context (no external API).
/// </summary>
public class LocalLlmService : ILlm
{
    public Task<TResponse> PromptAsync<TResponse>(IChatRequest request, CancellationToken cancellationToken)
        where TResponse : IChatResponse
    {
        if (request is not AzureOpenAIChatRequest chatRequest)
            throw new ArgumentException($"LocalLlmService expects {nameof(AzureOpenAIChatRequest)}.", nameof(request));

        ArgumentNullException.ThrowIfNull(chatRequest.Messages);

        string systemText = string.Empty;
        string userText = string.Empty;

        foreach (ChatMessage message in chatRequest.Messages)
        {
            string content = AzureOpenAIChatRequest.GetRequestContent(message);
            switch (message)
            {
                case SystemChatMessage:
                    systemText = content;
                    break;
                case UserChatMessage:
                    userText = content;
                    break;
            }
        }

        IChatResponse response = new AzureOpenAIChatResponse(ExtractiveChatClient.Compose(systemText, userText));
        return Task.FromResult((TResponse)response);
    }

    public async IAsyncEnumerable<string> StreamAsync(
        IChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        AzureOpenAIChatResponse response = await PromptAsync<AzureOpenAIChatResponse>(request, cancellationToken);
        foreach (string part in ExtractiveChatClient.SplitWords(response.Text ?? string.Empty))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return part;
        }
    }
}
