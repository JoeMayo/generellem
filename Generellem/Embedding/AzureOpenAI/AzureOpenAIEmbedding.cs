using Azure;

using Generellem.Document.DocumentTypes;
using Generellem.Llm;
using Generellem.Processors;
using Generellem.Rag;
using Generellem.Services;
using Generellem.Services.Exceptions;

using Microsoft.Extensions.Logging;

using OpenAI.Embeddings;

using Polly;

using System.ClientModel;

namespace Generellem.Embedding.AzureOpenAI;

public class AzureOpenAIEmbedding(
    ILlmClientFactory llmClientFact, 
    ILogger<AzureOpenAIEmbedding> logger)
    : IEmbedding
{
    static IProgress<IngestionProgress>? currentProgress = null;

    public ResiliencePipeline Pipeline { get; set; } =
        new ResiliencePipelineBuilder()
            .AddRetry(new()
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(ex => ex is not GenerellemNeedsIngestionException),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,  // Adds a random factor to the delay
                MaxRetryAttempts = 10,
                Delay = TimeSpan.FromSeconds(3),
                OnRetry = static args =>
                {
                    currentProgress?.Report(new("System busy, delaying a few seconds..."));
                    return default;
                }
            })
            .Build();

    /// <summary>
    /// Breaks text into chunks and adds an embedding to each chunk based on the text in that chunk.
    /// </summary>
    /// <param name="fullText">Full document text.</param>
    /// <param name="docType"><see cref="IDocumentType"/> for extracting text from document.</param>
    /// <param name="documentReference">Reference to file. e.g. either a path, url, or some other indicator of where the file came from.</param>
    /// <param name="progress">Reports progress.</param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
    /// <returns>List of <see cref="TextChunk"/></returns>
    public virtual async Task<List<TextChunk>> EmbedAsync(string fullText, IDocumentType docType, string documentReference, IProgress<IngestionProgress> progress, CancellationToken cancellationToken)
    {
        currentProgress = progress;

        List<TextChunk> chunks = TextProcessor.BreakIntoChunks(fullText, documentReference);

        int chunkCount = chunks.Count;
        int count = 0;
        string plural = chunkCount == 1 ? "" : "s";

        EmbeddingClient embeddingClient = llmClientFact.CreateEmbeddingClient();

        foreach (TextChunk chunk in chunks)
        {
            if (chunk.Content is null) continue;

            chunk.Order = ++count;
            progress.Report(new($"Processing {chunk.Order} of {chunkCount} chunk{plural}"));

            try
            {
                ClientResult<OpenAIEmbedding> embeddingResult = 
                    await Pipeline.ExecuteAsync(
                        async _ => await embeddingClient.GenerateEmbeddingAsync(chunk.Content),
                        cancellationToken);

                chunk.Embedding = embeddingResult.Value.ToFloats();
            }
            catch (RequestFailedException rfEx)
            {
                logger.LogError(GenerellemLogEvents.AuthorizationFailure, rfEx, "Please check credentials and exception details for more info.");
                throw;
            }
        }

        return chunks;
    }

    public async Task<ReadOnlyMemory<float>> GetEmbeddingAsync(string text, CancellationToken cancellationToken)
    {
        EmbeddingClient embedingClient = llmClientFact.CreateEmbeddingClient();

        ClientResult<OpenAIEmbedding> embeddingResult = 
            await Pipeline.ExecuteAsync(
                async _ => await embedingClient.GenerateEmbeddingAsync(text),
                cancellationToken);

        return embeddingResult.Value.ToFloats();
    }
}
