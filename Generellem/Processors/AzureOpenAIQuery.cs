using System.Runtime.CompilerServices;
using System.Text;

using Generellem.Embedding;
using Generellem.Llm;
using Generellem.Llm.AzureOpenAI;
using Generellem.Rag;

using OpenAI.Chat;

namespace Generellem.Processors;

/// <summary>
/// Orchestrates Retrieval-Augmented Generation (RAG)
/// </summary>
public class AzureOpenAIQuery(ILlm llm, IRag rag) : IGenerellemQuery
{
    /// <summary>
    /// Searches for context, builds a prompt, and gets a response from Azure OpenAI
    /// </summary>
    /// <typeparam name="TResponse">Type of response with all data returned from the LLM.</typeparam>
    /// <param name="queryText">User's request</param>
    /// <param name="chatHistory">History of questions asked/answered to add to context</param>
    /// <param name="cancelToken"><see cref="CancellationToken"/></param>
    /// <returns>Azure OpenAI response text</returns>
    public async Task<string> AskAsync(string queryText, Queue<ChatMessage> chatHistory, CancellationToken cancelToken)
    {
        QueryDetails<AzureOpenAIChatRequest, AzureOpenAIChatResponse> response = 
            await PromptAsync<AzureOpenAIChatRequest, AzureOpenAIChatResponse>(queryText, chatHistory, cancelToken);

        return WithSourceCitations(response.Response?.Text, response.Request?.TextChunks);
    }

    public async IAsyncEnumerable<string> AskStreamAsync(
        string queryText,
        Queue<ChatMessage> chatHistory,
        [EnumeratorCancellation] CancellationToken cancelToken)
    {
        AzureOpenAIChatRequest request =
            await rag.BuildRequestAsync<AzureOpenAIChatRequest>(queryText, chatHistory, cancelToken);

        StringBuilder body = new();
        await foreach (string token in llm.StreamAsync(request, cancelToken).ConfigureAwait(false))
        {
            body.Append(token);
            yield return token;
        }

        string withSources = WithSourceCitations(body.ToString(), request.TextChunks);
        if (withSources.Length > body.Length)
            yield return withSources[body.Length..];
    }

    /// <summary>
    /// Appends the distinct <see cref="TextChunk.DocumentReference"/> values used as context.
    /// </summary>
    internal static string WithSourceCitations(string? answer, IReadOnlyList<TextChunk>? chunks)
    {
        string text = answer ?? string.Empty;
        if (chunks is null || chunks.Count == 0)
            return text;

        List<string> sources = [];
        foreach (TextChunk chunk in chunks)
        {
            if (string.IsNullOrWhiteSpace(chunk.DocumentReference))
                continue;

            if (!sources.Contains(chunk.DocumentReference, StringComparer.Ordinal))
                sources.Add(chunk.DocumentReference);
        }

        if (sources.Count == 0)
            return text;

        StringBuilder sb = new();
        if (text.Length > 0)
        {
            sb.Append(text);
            sb.AppendLine();
            sb.AppendLine();
        }

        sb.AppendLine("Sources:");
        for (int i = 0; i < sources.Count; i++)
        {
            sb.Append("- ");
            if (i == sources.Count - 1)
                sb.Append(sources[i]);
            else
                sb.AppendLine(sources[i]);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Searches for context, builds a prompt, and gets a response from Azure OpenAI
    /// </summary>
    /// <typeparam name="TResponse">Type of response with all data returned from the LLM.</typeparam>
    /// <param name="queryText">User's request</param>
    /// <param name="chatHistory">History of questions asked/answered to add to context</param>
    /// <param name="cancelToken"><see cref="CancellationToken"/></param>
    /// <returns>Azure OpenAI response</returns>
    public virtual async Task<QueryDetails<TRequest, TResponse>> PromptAsync<TRequest, TResponse>(
        string queryText, Queue<ChatMessage> chatHistory, CancellationToken cancelToken)
        where TRequest : IChatRequest
        where TResponse : IChatResponse
    {
        AzureOpenAIChatRequest request = 
            await rag.BuildRequestAsync<AzureOpenAIChatRequest>(queryText, chatHistory, cancelToken);

        AzureOpenAIChatResponse response = await llm.PromptAsync<AzureOpenAIChatResponse>(request, cancelToken);

        return new QueryDetails<TRequest, TResponse>
        {
            Request = (TRequest)(IChatRequest)request,
            Response = (TResponse)(IChatResponse) response
        };
    }
}
