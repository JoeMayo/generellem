using Generellem.Embedding;

namespace Generellem.Services;

/// <summary>
/// Reciprocal rank fusion for vector and keyword hit lists.
/// </summary>
public static class HybridRank
{
    public const int RrfK = 60;

    public static List<TextChunk> Fuse(int topK, IReadOnlyList<TextChunk> vectorHits, IReadOnlyList<TextChunk> keywordHits)
    {
        Dictionary<string, Ranked> scores = new(StringComparer.Ordinal);
        Add(vectorHits);
        Add(keywordHits);

        int limit = Math.Max(1, topK);
        return scores.Values
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Order)
            .Take(limit)
            .Select(item => item.Chunk)
            .ToList();

        void Add(IReadOnlyList<TextChunk> hits)
        {
            for (int rank = 0; rank < hits.Count; rank++)
            {
                TextChunk chunk = hits[rank];
                string key = string.IsNullOrEmpty(chunk.ID)
                    ? (chunk.DocumentReference ?? string.Empty) + "#" + chunk.Order
                    : chunk.ID;
                double increment = 1d / (RrfK + rank + 1);
                if (scores.TryGetValue(key, out Ranked existing))
                    scores[key] = existing with { Score = existing.Score + increment };
                else
                    scores[key] = new Ranked(chunk, increment, scores.Count);
            }
        }
    }

    public static List<TextChunk> KeywordRank(string? query, IEnumerable<TextChunk> chunks, int limit)
    {
        if (LexicalText.Tokens(query).Count == 0)
            return [];

        return chunks
            .Select(chunk => (Chunk: chunk, Score: LexicalText.Overlap(query, chunk.Content)))
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Chunk.ID, StringComparer.Ordinal)
            .Take(Math.Max(1, limit))
            .Select(item => item.Chunk)
            .ToList();
    }

    readonly record struct Ranked(TextChunk Chunk, double Score, int Order);
}
