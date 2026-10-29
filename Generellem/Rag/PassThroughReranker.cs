using Generellem.Embedding;

namespace Generellem.Rag;

/// <summary>
/// Returns candidates in the order the search backend produced them.
/// </summary>
public sealed class PassThroughReranker : IReranker
{
    public Task<List<TextChunk>> RerankAsync(string query, IReadOnlyList<TextChunk> candidates, CancellationToken cancellationToken) =>
        Task.FromResult(candidates.ToList());
}
