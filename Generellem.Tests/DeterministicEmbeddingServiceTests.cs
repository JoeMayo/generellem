using Generellem.DependencyInjection;
using Generellem.Document.DocumentTypes;
using Generellem.Embedding;
using Generellem.Embedding.Deterministic;
using Generellem.Processors;

using Microsoft.Extensions.Options;

namespace Generellem.Embedding.Tests;

public class DeterministicEmbeddingServiceTests
{
    readonly DeterministicEmbeddingService sut;

    public DeterministicEmbeddingServiceTests()
    {
        GenerellemOptions options = new() { EmbeddingDimensions = 32 };
        sut = new DeterministicEmbeddingService(Options.Create(options));
    }

    [Fact]
    public async Task GetEmbeddingAsync_ReturnsConfiguredDimensions()
    {
        ReadOnlyMemory<float> embedding = await sut.GetEmbeddingAsync("hello world", CancellationToken.None);

        Assert.Equal(32, embedding.Length);
    }

    [Fact]
    public async Task GetEmbeddingAsync_IsDeterministic()
    {
        ReadOnlyMemory<float> a = await sut.GetEmbeddingAsync("same text", CancellationToken.None);
        ReadOnlyMemory<float> b = await sut.GetEmbeddingAsync("same text", CancellationToken.None);

        Assert.Equal(a.ToArray(), b.ToArray());
    }

    [Fact]
    public async Task GetEmbeddingAsync_SimilarTextIsCloserThanUnrelated()
    {
        ReadOnlyMemory<float> query = await sut.GetEmbeddingAsync("contribute to open source project", CancellationToken.None);
        ReadOnlyMemory<float> related = await sut.GetEmbeddingAsync("how to contribute to the open source project", CancellationToken.None);
        ReadOnlyMemory<float> unrelated = await sut.GetEmbeddingAsync("weather forecast tomorrow rain", CancellationToken.None);

        float relatedScore = Services.VectorMath.CosineSimilarity(query.Span, related.Span);
        float unrelatedScore = Services.VectorMath.CosineSimilarity(query.Span, unrelated.Span);

        Assert.True(relatedScore > unrelatedScore);
    }

    [Fact]
    public async Task EmbedAsync_PopulatesChunkEmbeddings()
    {
        Mock<IDocumentType> docType = new();
        Progress<IngestionProgress> progress = new();

        List<TextChunk> chunks = await sut.EmbedAsync(
            "Short document text for chunking.",
            docType.Object,
            "FileSystem@doc.txt",
            progress,
            CancellationToken.None);

        Assert.NotEmpty(chunks);
        Assert.All(chunks, c => Assert.Equal(32, c.Embedding.Length));
        Assert.All(chunks, c => Assert.False(string.IsNullOrEmpty(c.ID)));
    }
}
