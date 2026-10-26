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
/// Embeddings via local Ollama HTTP API.
/// </summary>
public class OllamaEmbedding(
    IHttpClientFactory httpClientFactory,
    IOptions<GenerellemOptions> options,
    ILogger<OllamaEmbedding> logger)
    : IEmbedding
{
    static IProgress<IngestionProgress>? currentProgress;

    readonly GenerellemOptions options = options.Value;

    public ResiliencePipeline Pipeline { get; set; } =
        new ResiliencePipelineBuilder()
            .AddRetry(new()
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                MaxRetryAttempts = 5,
                Delay = TimeSpan.FromSeconds(2),
                OnRetry = static args =>
                {
                    currentProgress?.Report(new("Ollama busy, retrying embedding..."));
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

        List<TextChunk> chunks = TextProcessor.BreakIntoChunks(fullText, fileName);

        int chunkCount = chunks.Count;
        int count = 0;
        string plural = chunkCount == 1 ? "" : "s";

        foreach (TextChunk chunk in chunks)
        {
            if (chunk.Content is null)
                continue;

            chunk.Order = ++count;
            progress.Report(new($"Processing {chunk.Order} of {chunkCount} chunk{plural}"));
            chunk.Embedding = await GetEmbeddingAsync(chunk.Content, cancellationToken);
        }

        return chunks;
    }

    public async Task<ReadOnlyMemory<float>> GetEmbeddingAsync(string text, CancellationToken cancellationToken)
    {
        try
        {
            using HttpClient client = httpClientFactory.Create();
            client.BaseAddress = new Uri(options.OllamaEndpoint.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromMinutes(5);

            OllamaEmbeddingRequest body = new()
            {
                Model = options.OllamaEmbeddingModel,
                Prompt = text
            };

            OllamaEmbeddingResponse? response = await Pipeline.ExecuteAsync(
                async token =>
                {
                    using HttpResponseMessage httpResponse = await client.PostAsJsonAsync("api/embeddings", body, token);
                    httpResponse.EnsureSuccessStatusCode();
                    return await httpResponse.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>(cancellationToken: token);
                },
                cancellationToken);

            if (response?.Embedding is null || response.Embedding.Count == 0)
                throw new InvalidOperationException("Ollama returned an empty embedding.");

            return response.Embedding.ToArray();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ollama embedding failed. Is Ollama running at {Endpoint} with model {Model}?",
                options.OllamaEndpoint, options.OllamaEmbeddingModel);
            throw;
        }
    }

    sealed class OllamaEmbeddingRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = string.Empty;
    }

    sealed class OllamaEmbeddingResponse
    {
        [JsonPropertyName("embedding")]
        public List<float>? Embedding { get; set; }
    }
}
