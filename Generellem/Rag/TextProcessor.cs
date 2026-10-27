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
    /// Breaks a string into chunks that prefer paragraph, then sentence, then word boundaries.
    /// A unit longer than <see cref="ChunkSize"/> is cut on characters.
    /// Overlap repeats the tail of the previous chunk unless that would cut a unit that otherwise fits.
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

        if (text.Length == 0)
            return [];

        int sourceRefSeparatorPos = documentReference.IndexOf('@');
        if (sourceRefSeparatorPos < 0)
            throw new ArgumentException("Document reference must contain a source reference separated by '@'.", nameof(documentReference));

        string sourceReference = documentReference[..sourceRefSeparatorPos];
        List<TextChunk> chunks = [];
        int consumed = 0;

        while (consumed < text.Length)
        {
            int overlapLen = consumed == 0 ? 0 : Math.Min(Overlap, consumed);
            int end = PackEnd(text, consumed, ChunkSize - overlapLen, ChunkSize);
            if (end <= consumed && overlapLen > 0)
            {
                overlapLen = 0;
                end = PackEnd(text, consumed, ChunkSize, ChunkSize);
            }

            if (end <= consumed)
                throw new InvalidOperationException("Text chunking failed to advance.");

            chunks.Add(
                new TextChunk
                {
                    ID = Guid.NewGuid().ToString(),
                    Content = text[(consumed - overlapLen)..end],
                    DocumentReference = documentReference,
                    SourceReference = sourceReference
                });

            consumed = end;
        }

        return chunks;
    }

    /// <summary>
    /// Exclusive end of the next chunk's new text. Returns <paramref name="start"/> when the caller
    /// should drop overlap so a paragraph, sentence, or word that fits in <paramref name="chunkSize"/> is kept whole.
    /// </summary>
    static int PackEnd(string text, int start, int budget, int chunkSize)
    {
        int remaining = text.Length - start;
        if (remaining <= 0)
            return start;

        if (remaining <= budget)
            return text.Length;

        int hardEnd = start + budget;
        int soft = LastSoftBoundary(text, start, hardEnd);
        if (soft > start)
            return soft;

        if (budget < chunkSize)
        {
            int widerSoft = LastSoftBoundary(text, start, Math.Min(start + chunkSize, text.Length));
            if (widerSoft > hardEnd)
                return start;
        }

        int word = LastWordEnd(text, start, hardEnd);
        if (word > start)
            return word;

        if (budget < chunkSize)
        {
            int widerWord = LastWordEnd(text, start, Math.Min(start + chunkSize, text.Length));
            if (widerWord > hardEnd)
                return start;
        }

        return hardEnd;
    }

    static int LastSoftBoundary(string text, int start, int hardEnd)
    {
        if (hardEnd <= start)
            return -1;

        ReadOnlySpan<char> window = text.AsSpan(start, hardEnd - start);
        bool reachesEnd = hardEnd == text.Length;
        int relative = Math.Max(LastParagraphEnd(window), LastSentenceEnd(window, reachesEnd));
        return relative > 0 ? start + relative : -1;
    }

    static int LastWordEnd(string text, int start, int hardEnd)
    {
        if (hardEnd <= start)
            return -1;

        int relative = LastWordEnd(text.AsSpan(start, hardEnd - start));
        return relative > 0 ? start + relative : -1;
    }

    static int LastParagraphEnd(ReadOnlySpan<char> window)
    {
        int best = -1;
        int index = 0;
        while (index < window.Length)
        {
            int separator = ParagraphSeparatorLength(window, index);
            if (separator == 0)
            {
                index++;
                continue;
            }

            int end = index + separator;
            while (end < window.Length && window[end] is '\n' or '\r')
                end++;

            if (end > 0)
                best = end;

            index = Math.Max(end, index + 1);
        }

        return best;
    }

    static int ParagraphSeparatorLength(ReadOnlySpan<char> window, int index)
    {
        if (index + 4 <= window.Length
            && window[index] == '\r' && window[index + 1] == '\n'
            && window[index + 2] == '\r' && window[index + 3] == '\n')
            return 4;

        if (index + 2 <= window.Length
            && window[index] == '\n' && window[index + 1] == '\n')
            return 2;

        return 0;
    }

    static int LastSentenceEnd(ReadOnlySpan<char> window, bool reachesTextEnd)
    {
        int best = -1;
        for (int i = 0; i < window.Length - 1; i++)
        {
            if (window[i] is not ('.' or '!' or '?'))
                continue;

            if (!char.IsWhiteSpace(window[i + 1]))
                continue;

            int end = i + 1;
            while (end < window.Length && char.IsWhiteSpace(window[end]))
                end++;

            if (end > 0)
                best = end;
        }

        // A sentence can end at the end of the document without trailing whitespace.
        // A period that only ends the window is not a boundary; the next character may continue it.
        if (reachesTextEnd && window.Length > 0)
        {
            int end = window.Length;
            while (end > 0 && char.IsWhiteSpace(window[end - 1]))
                end--;

            if (end > 0 && window[end - 1] is '.' or '!' or '?')
                best = window.Length;
        }

        return best;
    }

    static int LastWordEnd(ReadOnlySpan<char> window)
    {
        int best = -1;
        int index = 0;
        while (index < window.Length)
        {
            if (!char.IsWhiteSpace(window[index]))
            {
                index++;
                continue;
            }

            int end = index;
            while (end < window.Length && char.IsWhiteSpace(window[end]))
                end++;

            if (end > 0)
                best = end;

            index = Math.Max(end, index + 1);
        }

        return best;
    }
}