using Generellem.Embedding;

namespace Generellem.Rag;

/// <summary>
/// Reorders retrieved chunks before they are assembled into the prompt.
/// </summary>
public interface IReranker
{
    Task<List<TextChunk>> RerankAsync(string query, IReadOnlyList<TextChunk> candidates, CancellationToken cancellationToken);
}
