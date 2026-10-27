using Azure;

using Generellem.DependencyInjection;
using Generellem.Document.DocumentTypes;
using Generellem.Llm;
using Generellem.Processors;
using Generellem.Rag;
using Generellem.Services;
using Generellem.Services.Exceptions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpenAI.Embeddings;

using Polly;

using System.ClientModel;

namespace Generellem.Embedding.AzureOpenAI;

public class AzureOpenAIEmbedding(
    ILlmClientFactory llmClientFact, 
    ILogger<AzureOpenAIEmbedding> logger,
    TextProcessor textProcessor,
    IOptions<GenerellemOptions>? options = null)
    : IEmbedding
{
    readonly int embeddingBatchSize = options?.Value.EmbeddingBatchSize > 0
        ? options.Value.EmbeddingBatchSize
        : GenerellemOptions.DefaultEmbeddingBatchSize;

    IProgress<IngestionProgress>? currentProgress;
    ResiliencePipeline? pipeline;

    public ResiliencePipeline Pipeline
    {
        get => pipeline ??= CreatePipeline();
        set => pipeline = value;
    }

    ResiliencePipeline CreatePipeline() =>
        new ResiliencePipelineBuilder()
            .AddRetry(new()
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(ex => ex is not GenerellemNeedsIngestionException),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,  // Adds a random factor to the delay
                MaxRetryAttempts = 10,
                Delay = TimeSpan.FromSeconds(3),
                DelayGenerator = args => new ValueTask<TimeSpan?>(
                    EmbeddingRetryDelay.FromException(args.Outcome.Exception, args.AttemptNumber)),
                OnRetry = args =>
                {
                    string message = EmbeddingRetryDelay.FromException(args.Outcome.Exception, args.AttemptNumber) is null
                        ? "System busy, delaying a few seconds..."
                        : "Rate limit, delaying before the next embedding request...";
                    currentProgress?.Report(new(message));
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

        try
        {
            List<TextChunk> chunks = textProcessor.BreakIntoChunks(fullText, documentReference);
            List<TextChunk> pending = [];

            foreach (TextChunk chunk in chunks)
            {
                if (chunk.Content is null)
                    continue;

                chunk.Order = pending.Count + 1;
                pending.Add(chunk);
            }

            if (pending.Count == 0)
                return chunks;

            EmbeddingClient embeddingClient = llmClientFact.CreateEmbeddingClient();
            int batchSize = Math.Max(1, embeddingBatchSize);

            for (int offset = 0; offset < pending.Count; offset += batchSize)
            {
                int count = Math.Min(batchSize, pending.Count - offset);
                TextChunk[] batch = pending.GetRange(offset, count).ToArray();
                progress.Report(new($"Processing chunks {offset + 1}-{offset + count} of {pending.Count}"));

                string[] inputs = batch.Select(chunk => chunk.Content!).ToArray();

                try
                {
                    ClientResult<OpenAIEmbeddingCollection> embeddingResult =
                        await Pipeline.ExecuteAsync(
                            async token => await embeddingClient.GenerateEmbeddingsAsync(inputs, cancellationToken: token),
                            cancellationToken);

                    AssignEmbeddings(batch, embeddingResult.Value);
                }
                catch (RequestFailedException rfEx)
                {
                    logger.LogError(GenerellemLogEvents.AuthorizationFailure, rfEx, "Please check credentials and exception details for more info.");
                    throw;
                }
            }

            return chunks;
        }
        finally
        {
            currentProgress = null;
        }
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

    static void AssignEmbeddings(TextChunk[] batch, OpenAIEmbeddingCollection embeddings)
    {
        if (embeddings.Count != batch.Length)
            throw new InvalidOperationException(
                $"Embedding service returned {embeddings.Count} vectors for {batch.Length} chunks.");

        bool[] assigned = new bool[batch.Length];
        foreach (OpenAIEmbedding embedding in embeddings)
        {
            if ((uint)embedding.Index >= (uint)batch.Length || assigned[embedding.Index])
                throw new InvalidOperationException(
                    $"Embedding index {embedding.Index} is outside the batch of {batch.Length} or repeated.");

            assigned[embedding.Index] = true;
            batch[embedding.Index].Embedding = embedding.ToFloats();
        }
    }
}
