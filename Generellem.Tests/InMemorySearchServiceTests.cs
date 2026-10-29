using Generellem.DependencyInjection;
using Generellem.Embedding;
using Generellem.Services.InMemory;

using Microsoft.Extensions.Options;

namespace Generellem.Services.Tests;

public class InMemorySearchServiceTests
{
    readonly InMemorySearchService sut;

    public InMemorySearchServiceTests()
    {
        GenerellemOptions options = new() { EmbeddingDimensions = 4, TopK = 2 };
        sut = new InMemorySearchService(Options.Create(options));
    }

    [Fact]
    public async Task UploadAndSearch_ReturnsMostSimilarChunks()
    {
        await sut.UploadDocumentsAsync(
        [
            new TextChunk
            {
                ID = "a",
                Content = "alpha content",
                DocumentReference = "fs@a.txt",
                SourceReference = "fs",
                Embedding = new float[] { 1f, 0f, 0f, 0f }
            },
            new TextChunk
            {
                ID = "b",
                Content = "beta content",
                DocumentReference = "fs@b.txt",
                SourceReference = "fs",
                Embedding = new float[] { 0f, 1f, 0f, 0f }
            },
            new TextChunk
            {
                ID = "c",
                Content = "gamma content",
                DocumentReference = "fs@c.txt",
                SourceReference = "fs",
                Embedding = new float[] { 0.9f, 0.1f, 0f, 0f }
            }
        ], CancellationToken.None);

        List<TextChunk> results = await sut.SearchAsync(new float[] { 1f, 0f, 0f, 0f }, CancellationToken.None);

        Assert.Equal(2, results.Count);
        Assert.Equal("a", results[0].ID);
        Assert.Equal("c", results[1].ID);
        Assert.Equal("alpha content", results[0].Content);
        Assert.Empty(results[0].Embedding.ToArray());
    }

    [Fact]
    public async Task HybridSearch_PromotesKeywordMatch()
    {
        await sut.UploadDocumentsAsync(
        [
            new TextChunk
            {
                ID = "vector",
                Content = "zebra habitat",
                DocumentReference = "fs@vector.txt",
                Embedding = new float[] { 0f, 1f, 0f, 0f }
            },
            new TextChunk
            {
                ID = "keyword",
                Content = "alpha applesauce",
                DocumentReference = "fs@keyword.txt",
                Embedding = new float[] { 1f, 0f, 0f, 0f }
            }
        ], CancellationToken.None);

        List<TextChunk> results = await sut.SearchAsync("alpha", new float[] { 0f, 1f, 0f, 0f }, CancellationToken.None);

        Assert.Equal("keyword", results[0].ID);
    }

    [Fact]
    public async Task GetDocumentReferencesAsync_FiltersBySource()
    {
        await sut.UploadDocumentsAsync(
        [
            new TextChunk { ID = "1", DocumentReference = "fs@a", SourceReference = "fs", Content = "x", Embedding = new float[] { 1f, 0f, 0f, 0f } },
            new TextChunk { ID = "2", DocumentReference = "web@b", SourceReference = "web", Content = "y", Embedding = new float[] { 0f, 1f, 0f, 0f } }
        ], CancellationToken.None);

        List<TextChunk> fs = await sut.GetDocumentReferencesAsync("fs", CancellationToken.None);

        Assert.Single(fs);
        Assert.Equal("1", fs[0].ID);
    }

    [Fact]
    public async Task DeleteDocumentReferencesAsync_RemovesById()
    {
        await sut.UploadDocumentsAsync(
        [
            new TextChunk { ID = "del-me", DocumentReference = "fs@a", SourceReference = "fs", Content = "x", Embedding = new float[] { 1f, 0f, 0f, 0f } }
        ], CancellationToken.None);

        await sut.DeleteDocumentReferencesAsync(["del-me"], CancellationToken.None);

        List<TextChunk> remaining = await sut.GetDocumentReferenceAsync("fs@a", CancellationToken.None);
        Assert.Empty(remaining);
    }
}
