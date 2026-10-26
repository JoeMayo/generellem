using System.Security.Cryptography;
using System.Text;

using Generellem.DependencyInjection;
using Generellem.Document.DocumentTypes;
using Generellem.Processors;
using Generellem.Rag;

using Microsoft.Extensions.Options;

using Polly;

namespace Generellem.Embedding.Deterministic;

/// <summary>
/// Hash-based embeddings for offline demos. Similar text produces similar vectors without an API.
/// </summary>
public class DeterministicEmbeddingService(IOptions<GenerellemOptions> options) : IEmbedding
{
    readonly GenerellemOptions options = options.Value;

    public ResiliencePipeline Pipeline { get; set; } = new ResiliencePipelineBuilder().Build();

    public Task<List<TextChunk>> EmbedAsync(
        string fullText,
        IDocumentType docType,
        string fileName,
        IProgress<IngestionProgress> progress,
        CancellationToken cancellationToken)
    {
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
            chunk.Embedding = EmbedText(chunk.Content);
        }

        return Task.FromResult(chunks);
    }

    public Task<ReadOnlyMemory<float>> GetEmbeddingAsync(string text, CancellationToken cancellationToken) =>
        Task.FromResult(EmbedText(text));

    ReadOnlyMemory<float> EmbedText(string text)
    {
        int dimensions = Math.Max(1, options.EmbeddingDimensions);
        float[] vector = new float[dimensions];

        if (string.IsNullOrWhiteSpace(text))
            return vector;

        string[] tokens = text
            .ToLowerInvariant()
            .Split([' ', '\t', '\r', '\n', '.', ',', ';', ':', '!', '?', '(', ')', '[', ']', '{', '}', '"', '\''],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (string token in tokens)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            int index = (int)(BitConverter.ToUInt32(hash, 0) % (uint)dimensions);
            float sign = (hash[4] & 1) == 0 ? 1f : -1f;
            float weight = 1f + (hash[5] / 255f);
            vector[index] += sign * weight;
        }

        // Add character bigrams for short text stability
        string compact = text.ToLowerInvariant();
        for (int i = 0; i < compact.Length - 1; i++)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(compact.AsSpan(i, 2).ToString()));
            int index = (int)(BitConverter.ToUInt32(hash, 0) % (uint)dimensions);
            vector[index] += 0.25f * ((hash[4] & 1) == 0 ? 1f : -1f);
        }

        float magnitude = 0f;
        for (int i = 0; i < vector.Length; i++)
            magnitude += vector[i] * vector[i];

        magnitude = MathF.Sqrt(magnitude);
        if (magnitude > 0f)
        {
            for (int i = 0; i < vector.Length; i++)
                vector[i] /= magnitude;
        }

        return vector;
    }
}
