namespace Generellem.Services;

/// <summary>
/// Hashed bag-of-words sparse vector for Qdrant lexical search.
/// The same function is used at ingest and query time.
/// </summary>
public static class LexicalSparse
{
    public const string VectorName = "lexical";

    const uint Space = 1 << 18;

    public static (float[] Values, uint[] Indices) FromText(string? text)
    {
        Dictionary<uint, int> counts = [];
        foreach (string token in LexicalText.Tokens(text))
        {
            uint index = Hash(token) % Space;
            counts.TryGetValue(index, out int count);
            counts[index] = count + 1;
        }

        if (counts.Count == 0)
            return ([], []);

        uint[] indices = counts.Keys.OrderBy(index => index).ToArray();
        float[] values = new float[indices.Length];
        for (int i = 0; i < indices.Length; i++)
            values[i] = counts[indices[i]];

        return (values, indices);
    }

    static uint Hash(string token)
    {
        uint hash = 2166136261;
        foreach (char c in token)
        {
            hash ^= c;
            hash *= 16777619;
        }

        return hash;
    }
}
