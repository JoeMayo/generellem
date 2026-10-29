using Generellem.Embedding;
using Generellem.Services;

namespace Generellem.Rag;

/// <summary>
/// Orders chunks by how many query terms appear in each chunk.
/// Ties keep the original search order.
/// </summary>
public sealed class LexicalReranker : IReranker
{
    public Task<List<TextChunk>> RerankAsync(string query, IReadOnlyList<TextChunk> candidates, CancellationToken cancellationToken)
    {
        List<TextChunk> ordered = candidates
            .Select((chunk, index) => (Chunk: chunk, Score: LexicalText.Overlap(query, chunk.Content), Index: index))
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Index)
            .Select(item => item.Chunk)
            .ToList();

        return Task.FromResult(ordered);
    }
}
