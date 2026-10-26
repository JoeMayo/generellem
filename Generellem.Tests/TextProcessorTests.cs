using Generellem.Embedding;

namespace Generellem.Rag.Tests;

public class TextProcessorTests
{
    private const string DefaultDocumentReference = "mysource@file1.txt";

    public TextProcessorTests()
    {
        // Static settings are shared across tests; reset to documented defaults.
        TextProcessor.ChunkSize = 5000;
        TextProcessor.Overlap = 100;
    }

    [Fact]
    public void BreakIntoChunks_WithShortText_ReturnsOneChunk()
    {
        string? text = "Hello world";

        List<TextChunk> chunks = TextProcessor.BreakIntoChunks(text, DefaultDocumentReference);

        Assert.Single(chunks);
    }

    [Fact]
    public void BreakIntoChunks_WithLongText_ReturnsMultipleChunks()
    {
        string? text = new('a', 6000);

        List<TextChunk> chunks = TextProcessor.BreakIntoChunks(text, DefaultDocumentReference);

        Assert.True(chunks.Count > 1);
    }

    [Fact]
    public void BreakIntoChunks_WithLongText_ReturnsAppropriatelySizedChunks()
    {
        const int FirstChunkSize = 5000;
        const int SecondChunkSize = 1100;

        string? text = new('a', 6000);

        List<TextChunk> chunks = TextProcessor.BreakIntoChunks(text, DefaultDocumentReference);

        Assert.Equal(FirstChunkSize, chunks[0]?.Content?.Length);
        Assert.Equal(SecondChunkSize, chunks[1]?.Content?.Length);
    }

    [Fact]
    public void BreakIntoChunks_WithLongText_ReturnsOverlappedText()
    {
        string? text = new string('a', 4995) + "1234567890" + new string('b', 995);

        List<TextChunk> chunks = TextProcessor.BreakIntoChunks(text, DefaultDocumentReference);

        Assert.Equal(chunks[0]?.Content?[^100..], chunks[1]?.Content?[..100]);
    }

    [Fact]
    public void BreakIntoChunks_ChunksHaveCorrectContent()
    {
        string? text = new string('a', 4995) + "1234567890" + new string('b', 995);

        List<TextChunk> chunks = TextProcessor.BreakIntoChunks(text, DefaultDocumentReference);

        Assert.Equal(text, chunks[0]?.Content?[..^100] + chunks[1].Content);
    }
    
    [Fact]
    public void BreakIntoChunks_PopulatesReferences()
    {
        string? text = "Some content.";

        List<TextChunk> chunks = TextProcessor.BreakIntoChunks(text, DefaultDocumentReference);

        Assert.Equal("mysource", chunks[0].SourceReference);
        Assert.Equal("mysource@file1.txt", chunks[0].DocumentReference);
    }
    
    [Fact]
    public void BreakIntoChunks_WithMissingSourceReference_Throws()
    {
        string? text = "Some content.";

        Assert.Throws<ArgumentException>(() => TextProcessor.BreakIntoChunks(text, "invalid"));
    }
}
