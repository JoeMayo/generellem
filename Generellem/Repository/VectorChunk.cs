using Microsoft.EntityFrameworkCore;

namespace Generellem.Repository;

/// <summary>
/// Persisted text chunk + embedding for the Sqlite search backend.
/// </summary>
[Index(nameof(DocumentReference))]
[Index(nameof(SourceReference))]
public class VectorChunk
{
    /// <summary>
    /// Chunk ID (matches <see cref="Embedding.TextChunk.ID"/>).
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string? DocumentReference { get; set; }

    public string? SourceReference { get; set; }

    public string? Content { get; set; }

    /// <summary>
    /// Little-endian float32 embedding bytes.
    /// </summary>
    public byte[]? Embedding { get; set; }

    public int Order { get; set; }
}
