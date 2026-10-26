using Generellem.Embedding;
using Generellem.Services.Qdrant;

using Microsoft.Extensions.Logging;

namespace Generellem.Services.Tests;

public class QdrantSearchParityTests
{
    [Fact]
    public void FilterPayloadFields_IncludeSourceTenantGroupAndPath()
    {
        Assert.Contains("SourceReference", QdrantService.FilterPayloadFields);
        Assert.Contains("TenantID", QdrantService.FilterPayloadFields);
        Assert.Contains("GroupID", QdrantService.FilterPayloadFields);
        Assert.Contains("Path", QdrantService.FilterPayloadFields);
        Assert.Contains("DocumentReference", QdrantService.FilterPayloadFields);
    }

    [Fact]
    public async Task SearchBySourceReferenceAsync_LooksUpThatSource()
    {
        RecordingQdrant sut = new();

        List<TextChunk> result = await sut.SearchBySourceReferenceAsync("fs", CancellationToken.None);

        Assert.Equal("fs", sut.Source);
        Assert.Equal("q1", Assert.Single(result).ID);
    }

    sealed class RecordingQdrant : QdrantService
    {
        public string? Source { get; private set; }

        public RecordingQdrant()
            : base(Mock.Of<IDynamicConfiguration>(), Mock.Of<ILogger<QdrantService>>())
        {
        }

        public override Task<List<TextChunk>> GetDocumentReferencesAsync(string docSourcePrefix, CancellationToken cancellationToken)
        {
            Source = docSourcePrefix;
            return Task.FromResult<List<TextChunk>>([new() { ID = "q1", DocumentReference = "fs@a.txt" }]);
        }
    }
}