using Generellem.DependencyInjection;
using Generellem.Embedding;

using Microsoft.Extensions.Options;

namespace Generellem.Rag.Tests;

public class TextProcessorTests
{
    private const string DefaultDocumentReference = "mysource@file1.txt";

    static TextProcessor Create(int chunkSize = 5000, int overlap = 100) =>
        new(Options.Create(new GenerellemOptions
        {
            ChunkSize = chunkSize,
            ChunkOverlap = overlap
        }));

    [Fact]
    public void BreakIntoChunks_WithShortText_ReturnsOneChunk()
    {
        string? text = "Hello world";

        List<TextChunk> chunks = Create().BreakIntoChunks(text, DefaultDocumentReference);

        Assert.Single(chunks);
    }

    [Fact]
    public void BreakIntoChunks_WithLongText_ReturnsMultipleChunks()
    {
        string? text = new('a', 6000);

        List<TextChunk> chunks = Create().BreakIntoChunks(text, DefaultDocumentReference);

        Assert.True(chunks.Count > 1);
    }

    [Fact]
    public void BreakIntoChunks_WithLongText_ReturnsAppropriatelySizedChunks()
    {
        const int FirstChunkSize = 5000;
        const int SecondChunkSize = 1100;

        string? text = new('a', 6000);

        List<TextChunk> chunks = Create().BreakIntoChunks(text, DefaultDocumentReference);

        Assert.Equal(FirstChunkSize, chunks[0]?.Content?.Length);
        Assert.Equal(SecondChunkSize, chunks[1]?.Content?.Length);
    }

    [Fact]
    public void BreakIntoChunks_WithLongText_ReturnsOverlappedText()
    {
        string? text = new string('a', 4995) + "1234567890" + new string('b', 995);

        List<TextChunk> chunks = Create().BreakIntoChunks(text, DefaultDocumentReference);

        Assert.Equal(chunks[0]?.Content?[^100..], chunks[1]?.Content?[..100]);
    }

    [Fact]
    public void BreakIntoChunks_ChunksHaveCorrectContent()
    {
        string? text = new string('a', 4995) + "1234567890" + new string('b', 995);

        List<TextChunk> chunks = Create().BreakIntoChunks(text, DefaultDocumentReference);

        Assert.Equal(text, chunks[0]?.Content?[..^100] + chunks[1].Content);
    }

    [Fact]
    public void BreakIntoChunks_PopulatesReferences()
    {
        string? text = "Some content.";

        List<TextChunk> chunks = Create().BreakIntoChunks(text, DefaultDocumentReference);

        Assert.Equal("mysource", chunks[0].SourceReference);
        Assert.Equal("mysource@file1.txt", chunks[0].DocumentReference);
    }

    [Fact]
    public void BreakIntoChunks_WithMissingSourceReference_Throws()
    {
        string? text = "Some content.";

        Assert.Throws<ArgumentException>(() => Create().BreakIntoChunks(text, "invalid"));
    }

    [Fact]
    public void BreakIntoChunks_UsesConfiguredChunkSize()
    {
        string text = new('a', 25);

        List<TextChunk> chunks = Create(chunkSize: 10, overlap: 0).BreakIntoChunks(text, DefaultDocumentReference);

        Assert.Equal(3, chunks.Count);
        Assert.Equal(10, chunks[0].Content?.Length);
        Assert.Equal(5, chunks[2].Content?.Length);
    }

    [Fact]
    public void BreakIntoChunks_WhenOverlapIsNotSmallerThanChunkSize_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Create(chunkSize: 10, overlap: 10).BreakIntoChunks("hello", DefaultDocumentReference));
    }

    [Fact]
    public void BreakIntoChunks_SplitsOnParagraphBoundary()
    {
        const string First = "Alpha stays.\n\n";
        const string Second = "Beta is longer than the remaining budget of the first chunk.";
        TextProcessor processor = Create(chunkSize: First.Length + 10, overlap: 0);

        List<TextChunk> chunks = processor.BreakIntoChunks(First + Second, DefaultDocumentReference);

        Assert.Equal(First, chunks[0].Content);
        Assert.StartsWith("Beta", chunks[1].Content);
        Assert.DoesNotContain("Beta", chunks[0].Content);
    }

    [Fact]
    public void BreakIntoChunks_SplitsOnCrLfParagraphBoundary()
    {
        const string First = "Alpha stays.\r\n\r\n";
        const string Second = "Beta is longer than the remaining budget of the first chunk.";
        TextProcessor processor = Create(chunkSize: First.Length + 10, overlap: 0);

        List<TextChunk> chunks = processor.BreakIntoChunks(First + Second, DefaultDocumentReference);

        Assert.Equal(First, chunks[0].Content);
        Assert.DoesNotContain("Beta", chunks[0].Content);
    }

    [Fact]
    public void BreakIntoChunks_PacksSentencesUntilTheChunkIsFull()
    {
        const string Packed = "One. Two. ";
        const string Next = "Three continues past the end.";
        TextProcessor processor = Create(chunkSize: Packed.Length + 6, overlap: 0);

        List<TextChunk> chunks = processor.BreakIntoChunks(Packed + Next, DefaultDocumentReference);

        Assert.Equal(Packed, chunks[0].Content);
        Assert.StartsWith("Three", chunks[1].Content);
        Assert.DoesNotContain("Three", chunks[0].Content);
    }

    [Fact]
    public void BreakIntoChunks_SplitsOnWordWhenTheSentenceDoesNotFit()
    {
        const string Word = "Supercalifragilistic ";
        const string Rest = "is the word.";
        TextProcessor processor = Create(chunkSize: Word.Length, overlap: 0);

        List<TextChunk> chunks = processor.BreakIntoChunks(Word + Rest, DefaultDocumentReference);

        Assert.Equal(Word, chunks[0].Content);
        Assert.Equal(Rest, chunks[1].Content);
    }

    [Fact]
    public void BreakIntoChunks_DropsOverlapRatherThanCuttingASentenceThatFits()
    {
        const string First = "Hi. ";
        const string Second = "Second sentence fits.";
        TextProcessor processor = Create(chunkSize: Second.Length, overlap: 4);

        List<TextChunk> chunks = processor.BreakIntoChunks(First + Second, DefaultDocumentReference);

        Assert.Equal(First, chunks[0].Content);
        Assert.Equal(Second, chunks[1].Content);
    }
}