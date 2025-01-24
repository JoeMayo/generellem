using Azure;

using Generellem.Embedding;
using Generellem.Llm;
using Generellem.Llm.AzureOpenAI;
using Generellem.Processors;
using Generellem.Services;

using Microsoft.Extensions.Logging;

using OpenAI.Chat;

using System.Text;

namespace Generellem.Rag;

/// <summary>
/// Performs Retrieval-Augmented Generation (RAG).
/// </summary>
/// <remarks>
/// Inspired by Retrieval-Augmented Generation (RAG)/Bea Stollnitz at https://bea.stollnitz.com/blog/rag/
/// </remarks>
public class GenerellemDefaultRag(
    ISearchService searchSvc,
    IDynamicConfiguration config,
    IEmbedding embedding,
    ILlm llm,
    ILogger<GenerellemDefaultRag> logger)
    : IRag
{
    /// <summary>
    /// We use this to summarize the user query, based on recent context.
    /// </summary>
    string ContextMessage { get; set; } =
        "You're an AI assistant reading the transcript of a conversation " +
        "between a user and an assistant. Given the chat history and " +
        "user's query, infer the user's real intent. Don't include any " +
        "discussion of whether you know what Generellem is - just assume " +
        "that Generellem is defined as this service that is asking you" +
        "questions.";

    /// <summary>
    /// Instructions to the LLM on how interpret query and respond.
    /// </summary>
    public string SystemMessage { get; set; } =
        "You are a professional AI bot that returns accurate content for busy workers.\n" +
        "Please answer the user's question using only information you can find in the context.\n" +
        "If the user's question is unrelated to the information in the context, say you don't know.\n";

    /// <summary>
    /// Tradeoff in accuracy vs creativity - ranges from 0 to 2 in OpenAI
    /// </summary>
    public float Temperature { get; set; } = 0;

    /// <summary>
    /// Builds a request based on prompt content.
    /// </summary>
    /// <param name="queryText">Text from user.</param>
    /// <param name="chatHistory">Previous queries in this thread.</param>
    /// <param name="cancelToken"><see cref="CancellationToken"/></param>
    /// <returns>Full request that can be sent to the LLM.</returns>
    public async Task<TRequest> BuildRequestAsync<TRequest>(string queryText, Queue<ChatMessage> chatHistory, CancellationToken cancelToken)
        where TRequest : IChatRequest, new()
    {
        AzureOpenAIChatRequest request = new();

        string? deploymentName = config[GKeys.AzOpenAIDeploymentName];
        ArgumentException.ThrowIfNullOrWhiteSpace(deploymentName, nameof(deploymentName));

        request.SummarizedUserIntent = await SummarizeUserIntentAsync(queryText, chatHistory, deploymentName, cancelToken);
        string summarizedIntentMessage = request.SummarizedUserIntent.Response?.Text ?? string.Empty;

        string context = await BuildContext(request, summarizedIntentMessage, cancelToken);

        UserChatMessage userQuery = new(queryText);

        request.Messages =
        [
            new SystemChatMessage(SystemMessage + "\n" + context),
            userQuery
        ];

        request.Options =
            new ChatCompletionOptions
            {
                Temperature = Temperature
            };

        return (TRequest)(IChatRequest)request;
    }

    protected virtual async Task<string> BuildContext(AzureOpenAIChatRequest request, string summarizedIntentMessage, CancellationToken cancelToken)
    {
        request.TextChunks = await SearchAsync(summarizedIntentMessage, cancelToken);

        foreach (TextChunk chunk in request.TextChunks)
            chunk.Embedding = null;

        string context =
            "\nContext: \n\n" +
            "```" +
            string.Join("\n\n", request.TextChunks.Select(c => c.Content)) +
            "```\n";
        return context;
    }

    /// <summary>
    /// Asks the LLM to clarify what the user wants to accomplish based on current query and chat history.
    /// </summary>
    /// <param name="userQuery">The question that the user is asking.</param>
    /// <param name="chatHistory">Context with previous questions the user asked.</param>
    /// <param name="deploymentName">Name of deployed LLM that we're using.</param>
    /// <param name="cancelToken"><see cref="CancellationToken"/></param>
    /// <returns><see cref="QueryDetails{TRequest, TResponse}"/> including clarification of user query, based on context.</returns>
    protected virtual async Task<QueryDetails<AzureOpenAIChatRequest, AzureOpenAIChatResponse>> SummarizeUserIntentAsync(
        string userQuery, Queue<ChatMessage> chatHistory, string deploymentName, CancellationToken cancelToken)
    {
        StringBuilder sb = new();

        foreach (ChatMessage chatMessage in chatHistory)
        {
            string role = chatMessage is UserChatMessage ? "User" : "System";
            sb.AppendLine($"{role}: {chatMessage.Content}\n");
        }

        List<ChatMessage> messages =
        [
            new SystemChatMessage(
                ContextMessage +
                $"\n\nChat History: {sb}" +
                $"\n\nUser's query: {userQuery}")
        ];

        ChatCompletionOptions chatCompletionOptions = new();
        AzureOpenAIChatRequest request = new()
        {
            Messages = messages,
            Options = chatCompletionOptions
        };

        AzureOpenAIChatResponse lastResponse = await llm.PromptAsync<AzureOpenAIChatResponse>(request, cancelToken);

        return new QueryDetails<AzureOpenAIChatRequest, AzureOpenAIChatResponse>()
        {
            Request = request,
            Response = lastResponse
        };
    }

    /// <summary>
    /// Performs Vector Search for chunks matching given text.
    /// </summary>
    /// <param name="text">Text for searching for matches.</param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
    /// <returns>List of text chunks matching query.</returns>
    public virtual async Task<List<TextChunk>> SearchAsync(string text, CancellationToken cancellationToken)
    {
        try
        {
            ReadOnlyMemory<float> embeddingVector = await embedding.GetEmbeddingAsync(text, cancellationToken);

            List<TextChunk> chunks = await searchSvc.SearchAsync(embeddingVector, cancellationToken);

            return chunks;
        }
        catch (RequestFailedException rfEx)
        {
            logger.LogError(GenerellemLogEvents.AuthorizationFailure, rfEx, "Please check credentials and exception details for more info.");
            throw;
        }
    }
}
