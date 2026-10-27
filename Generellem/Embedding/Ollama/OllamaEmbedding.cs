using System.Net.Http.Json;
using System.Text.Json.Serialization;

using Generellem.DependencyInjection;
using Generellem.Document.DocumentTypes;
using Generellem.Processors;
using Generellem.Rag;
using Generellem.Services;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Polly;

namespace Generellem.Embedding.Ollama;

/// <summary>
/// Embeddings via the local Ollama <c>/api/embed</c> endpoint.
/// Ingestion sends chunks in batches sized by <see cref="GenerellemOptions.EmbeddingBatchSize"/>.
/// </summary>
public class OllamaEmbedding(
    IGenerellemHttpClientFactory httpClientFactory,
    IOptions<GenerellemOptions> options,
    ILogger<OllamaEmbedding> logger,
    TextProcessor textProcessor)
    : IEmbedding
{
    IProgress<IngestionProgress>? currentProgress;
    ResiliencePipeline? pipeline;

    readonly GenerellemOptions options = options.Value;

    public ResiliencePipeline Pipeline
    {
        get => pipeline ??= CreatePipeline();
        set => pipeline = value;
    }

    ResiliencePipeline CreatePipeline() =>
        new ResiliencePipelineBuilder()
            .AddRetry(new()
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                MaxRetryAttempts = 5,
                Delay = TimeSpan.FromSeconds(2),
                DelayGenerator = args => new ValueTask<TimeSpan?>(
                    EmbeddingRetryDelay.FromException(args.Outcome.Exception, args.AttemptNumber)),
                OnRetry = args =>
                {
                    string message = EmbeddingRetryDelay.FromException(args.Outcome.Exception, args.AttemptNumber) is null
                        ? "Ollama busy, retrying embedding..."
                        : "Ollama rate limit, retrying embedding...";
                    currentProgress?.Report(new(message));
                    return default;
                }
            })
            .Build();

    public async Task<List<TextChunk>> EmbedAsync(
        string fullText,
        IDocumentType docType,
        string fileName,
        IProgress<IngestionProgress> progress,
        CancellationToken cancellationToken)
    {
        currentProgress = progress;

        try
        {
            List<TextChunk> chunks = textProcessor.BreakIntoChunks(fullText, fileName);
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

            using HttpClient client = CreateClient();
            int batchSize = Math.Max(1, options.EmbeddingBatchSize);

            for (int offset = 0; offset < pending.Count; offset += batchSize)
            {
                int count = Math.Min(batchSize, pending.Count - offset);
                List<TextChunk> batch = pending.GetRange(offset, count);
                progress.Report(new($"Processing chunks {offset + 1}-{offset + count} of {pending.Count}"));

                List<float[]> vectors = await RequestEmbeddingsAsync(
                    client,
                    batch.Select(chunk => chunk.Content!).ToArray(),
                    cancellationToken);

                for (int i = 0; i < batch.Count; i++)
                    batch[i].Embedding = vectors[i];
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
        using HttpClient client = CreateClient();
        List<float[]> vectors = await RequestEmbeddingsAsync(client, [text], cancellationToken);
        return vectors[0];
    }

    HttpClient CreateClient()
    {
        HttpClient client = httpClientFactory.Create();
        client.BaseAddress = new Uri(options.OllamaEndpoint.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromMinutes(5);
        return client;
    }

    async Task<List<float[]>> RequestEmbeddingsAsync(HttpClient client, IReadOnlyList<string> inputs, CancellationToken cancellationToken)
    {
        OllamaEmbedRequest body = new()
        {
            Model = options.OllamaEmbeddingModel,
            Input = inputs.ToList()
        };

        try
        {
            OllamaEmbedResponse? response = await Pipeline.ExecuteAsync(
                async token =>
                {
                    using HttpResponseMessage httpResponse = await client.PostAsJsonAsync("api/embed", body, token);
                    httpResponse.EnsureSuccessStatusCode();
                    return await httpResponse.Content.ReadFromJsonAsync<OllamaEmbedResponse>(cancellationToken: token);
                },
                cancellationToken);

            if (response?.Embeddings is null || response.Embeddings.Count != inputs.Count)
                throw new InvalidOperationException(
                    $"Ollama returned {response?.Embeddings?.Count ?? 0} embeddings for {inputs.Count} inputs.");

            List<float[]> vectors = new(response.Embeddings.Count);
            foreach (List<float>? vector in response.Embeddings)
            {
                if (vector is null || vector.Count == 0)
                    throw new InvalidOperationException("Ollama returned an empty embedding.");

                vectors.Add([.. vector]);
            }

            return vectors;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ollama embedding failed. Is Ollama running at {Endpoint} with model {Model}?",
                options.OllamaEndpoint, options.OllamaEmbeddingModel);
            throw;
        }
    }

    sealed class OllamaEmbedRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("input")]
        public List<string> Input { get; set; } = [];
    }

    sealed class OllamaEmbedResponse
    {
        [JsonPropertyName("embeddings")]
        public List<List<float>>? Embeddings { get; set; }
    }
}
