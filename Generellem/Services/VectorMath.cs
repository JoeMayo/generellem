namespace Generellem.Services;

/// <summary>
/// Shared vector helpers for local search backends.
/// </summary>
public static class VectorMath
{
    /// <summary>
    /// Cosine similarity between two equal-length vectors. Returns 0 if lengths differ or either is empty.
    /// </summary>
    public static float CosineSimilarity(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        if (a.Length == 0 || b.Length == 0 || a.Length != b.Length)
            return 0f;

        float dot = 0f;
        float magA = 0f;
        float magB = 0f;

        for (int i = 0; i < a.Length; i++)
        {
            float ai = a[i];
            float bi = b[i];
            dot += ai * bi;
            magA += ai * ai;
            magB += bi * bi;
        }

        float denom = MathF.Sqrt(magA) * MathF.Sqrt(magB);
        return denom == 0f ? 0f : dot / denom;
    }

    /// <summary>
    /// Serializes a float vector to little-endian bytes for SQLite storage.
    /// </summary>
    public static byte[] ToBytes(ReadOnlyMemory<float> embedding)
    {
        ReadOnlySpan<float> span = embedding.Span;
        byte[] bytes = new byte[span.Length * sizeof(float)];
        for (int i = 0; i < span.Length; i++)
        {
            byte[] floatBytes = BitConverter.GetBytes(span[i]);
            Buffer.BlockCopy(floatBytes, 0, bytes, i * sizeof(float), sizeof(float));
        }
        return bytes;
    }

    /// <summary>
    /// Deserializes little-endian float bytes to a vector.
    /// </summary>
    public static ReadOnlyMemory<float> FromBytes(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0 || bytes.Length % sizeof(float) != 0)
            return ReadOnlyMemory<float>.Empty;

        int count = bytes.Length / sizeof(float);
        float[] floats = new float[count];
        for (int i = 0; i < count; i++)
            floats[i] = BitConverter.ToSingle(bytes, i * sizeof(float));

        return floats;
    }
}
