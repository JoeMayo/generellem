using System.Collections.Concurrent;

using Generellem.DependencyInjection;
using Generellem.Embedding;

using Microsoft.Extensions.Options;

namespace Generellem.Services.InMemory;

/// <summary>
/// In-process vector store for local demos and CI. Register as singleton so state survives DI scopes.
/// </summary>
public class InMemorySearchService(IOptions<GenerellemOptions> options) : ISearchService
{
    readonly ConcurrentDictionary<string, TextChunk> chunks = new(StringComparer.Ordinal);
    readonly GenerellemOptions options = options.Value;

    public Task CreateIndexAsync(CancellationToken cancelToken) => Task.CompletedTask;

    public Task DeleteAllAsync(int companyId, CancellationToken cancellationToken)
    {
        chunks.Clear();
        return Task.CompletedTask;
    }

    public Task DeleteDocumentReferencesAsync(List<string> documentReferencesToDelete, CancellationToken cancellationToken)
    {
        foreach (string id in documentReferencesToDelete)
            chunks.TryRemove(id, out _);

        return Task.CompletedTask;
    }

    public Task<List<TextChunk>> GetDocumentReferenceAsync(string documentReference, CancellationToken cancellationToken)
    {
        List<TextChunk> result =
            chunks.Values
                .Where(c => string.Equals(c.DocumentReference, documentReference, StringComparison.Ordinal))
                .Select(CloneWithoutEmbedding)
                .ToList();

        return Task.FromResult(result);
    }

    public Task<List<TextChunk>> GetDocumentReferencesAsync(string sourceReference, CancellationToken cancellationToken)
    {
        List<TextChunk> result =
            chunks.Values
                .Where(c =>
                    string.Equals(c.SourceReference, sourceReference, StringComparison.Ordinal) ||
                    (c.DocumentReference?.StartsWith(sourceReference, StringComparison.Ordinal) ?? false))
                .Select(CloneWithoutEmbedding)
                .ToList();

        return Task.FromResult(result);
    }

    public Task<List<TextChunk>> GetDocumentReferencesByPathAsync(string path, CancellationToken cancellationToken)
    {
        List<TextChunk> result =
            chunks.Values
                .Where(c => c.DocumentReference?.Contains(path, StringComparison.OrdinalIgnoreCase) ?? false)
                .Select(CloneWithoutEmbedding)
                .ToList();

        return Task.FromResult(result);
    }

    public Task UploadDocumentsAsync(List<TextChunk> documents, CancellationToken cancelToken)
    {
        foreach (TextChunk doc in documents)
        {
            if (string.IsNullOrWhiteSpace(doc.ID))
                doc.ID = Guid.NewGuid().ToString();

            chunks[doc.ID] = new TextChunk
            {
                ID = doc.ID,
                Content = doc.Content,
                DocumentReference = doc.DocumentReference,
                SourceReference = doc.SourceReference,
                Order = doc.Order,
                Embedding = doc.Embedding.ToArray()
            };
        }

        return Task.CompletedTask;
    }

    public Task<List<TextChunk>> SearchAsync(ReadOnlyMemory<float> embedding, CancellationToken cancelToken)
    {
        int topK = Math.Max(1, options.TopK);

        List<TextChunk> result =
            chunks.Values
                .Select(c => (Chunk: c, Score: VectorMath.CosineSimilarity(embedding.Span, c.Embedding.Span)))
                .OrderByDescending(x => x.Score)
                .Take(topK)
                .Select(x => new TextChunk
                {
                    ID = x.Chunk.ID,
                    Content = x.Chunk.Content,
                    DocumentReference = x.Chunk.DocumentReference,
                    SourceReference = x.Chunk.SourceReference,
                    Order = x.Chunk.Order
                })
                .ToList();

        return Task.FromResult(result);
    }

    public Task<List<TextChunk>> SearchBySourceReferenceAsync(string sourceReference, CancellationToken cancellationToken) =>
        GetDocumentReferencesAsync(sourceReference, cancellationToken);

    static TextChunk CloneWithoutEmbedding(TextChunk c) =>
        new()
        {
            ID = c.ID,
            Content = c.Content,
            DocumentReference = c.DocumentReference,
            SourceReference = c.SourceReference,
            Order = c.Order
        };
}
