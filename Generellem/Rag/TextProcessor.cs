using Generellem.DependencyInjection;
using Generellem.Embedding;

using Microsoft.Extensions.Options;

namespace Generellem.Rag;

/// <summary>
/// Various methods for processing text.
/// </summary>
public class TextProcessor(IOptions<GenerellemOptions> options)
{
    readonly GenerellemOptions options = options.Value;

    /// <summary>
    /// Maximum size per text chunk.
    /// </summary>
    public int ChunkSize => options.ChunkSize;

    /// <summary>
    /// Number of characters to overlap between chunks.
    /// </summary>
    public int Overlap => options.ChunkOverlap;

    /// <summary>
    /// Breaks a string into chunks with overlap.
    /// </summary>
    /// <param name="text">Full text string to break apart.</param>
    /// <param name="documentReference">Reference to file. e.g. either a path, url, or some other indicator of where the file came from</param>
    /// <returns>List of <see cref="TextChunk"/> representing input text.</returns>
    public List<TextChunk> BreakIntoChunks(string text, string documentReference)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (ChunkSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(ChunkSize), ChunkSize, "Chunk size must be greater than 0.");

        if (Overlap < 0 || Overlap >= ChunkSize)
            throw new ArgumentOutOfRangeException(nameof(Overlap), Overlap, "Chunk overlap must be zero or greater and less than chunk size.");

        List<TextChunk> chunks = [];

        int chunkSize = Math.Min(text.Length, Math.Max(0, ChunkSize));
        int overlap = chunkSize < Overlap ? 0 : Overlap;
        int step = chunkSize - overlap;
        if (step <= 0)
            step = Math.Max(chunkSize, 1);

        for (int i = 0; i < text.Length; i += step)
        {
            int start = i;
            int end = Math.Min(i + ChunkSize, text.Length);

            string content = text[start..end];

            int sourceRefSeparatorPos = documentReference.IndexOf("@");
            if (sourceRefSeparatorPos < 0)
                throw new ArgumentException("Document reference must contain a source reference separated by '@'.", nameof(documentReference));
            string sourceReference = documentReference.Substring(0, sourceRefSeparatorPos);

            chunks.Add(
                new TextChunk()
                {
                    ID = Guid.NewGuid().ToString(),
                    Content = content,
                    DocumentReference = documentReference,
                    SourceReference = sourceReference
                });
        }

        return chunks;
    }
}