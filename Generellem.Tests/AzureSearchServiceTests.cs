using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;

using Generellem.Embedding;
using Generellem.Services.Azure;
using Generellem.Services.Qdrant;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Generellem.Services.Tests;

public class AzureSearchSchemaTests
{
    [Fact]
    public void CreateFields_IncludesQdrantFilterFields()
    {
        List<SearchField> fields = AzureSearchSchema.CreateFields(1536, "generellem-vector-profile");

        Assert.Contains(fields, field => field.Name == nameof(TextChunk.ID) && field.IsKey == true);
        Assert.Contains(fields, field => field.Name == nameof(TextChunk.Embedding));

        foreach (string name in AzureSearchSchema.FilterFields)
        {
            SearchField match = Assert.Single(fields, field => field.Name == name);
            Assert.True(match.IsFilterable == true);
        }
    }

    [Fact]
    public void FilterFields_AreIndexedByQdrant()
    {
        foreach (string name in AzureSearchSchema.FilterFields)
            Assert.Contains(name, QdrantService.FilterPayloadFields);
    }

    [Fact]
    public void SourceReferenceFilter_ScopesTenantAndGroupAndEscapesQuotes()
    {
        string filter = AzureSearchSchema.SourceReferenceFilter("o'reilly", "7", "2");

        Assert.Equal("SourceReference eq 'o''reilly' and TenantID eq '7' and GroupID eq '2'", filter);
    }

    [Fact]
    public void PathFilter_MatchesStoredPath()
    {
        string filter = AzureSearchSchema.PathFilter("/docs", "0", "0");

        Assert.Equal("Path eq '/docs' and TenantID eq '0' and GroupID eq '0'", filter);
    }

    [Fact]
    public void TenantFilter_UsesCompanyId()
    {
        Assert.Equal("TenantID eq '42'", AzureSearchSchema.TenantFilter(42));
    }

    [Fact]
    public void ToDocument_CopiesChunkAndScopeFields()
    {
        TextChunk chunk = new()
        {
            ID = "1",
            Content = "alpha",
            DocumentReference = "fs@a.txt",
            SourceReference = "fs",
            Embedding = new float[] { 1f, 0f }
        };

        SearchDocument document = AzureSearchSchema.ToDocument(chunk, "9", "3", "/drive");

        Assert.Equal("1", document[nameof(TextChunk.ID)]);
        Assert.Equal("alpha", document[nameof(TextChunk.Content)]);
        Assert.Equal("fs@a.txt", document[nameof(TextChunk.DocumentReference)]);
        Assert.Equal("fs", document[AzureSearchSchema.SourceReference]);
        Assert.Equal("/drive", document[AzureSearchSchema.Path]);
        Assert.Equal("9", document[AzureSearchSchema.TenantId]);
        Assert.Equal("3", document[AzureSearchSchema.GroupId]);
        Assert.Equal([1f, 0f], Assert.IsType<float[]>(document[nameof(TextChunk.Embedding)]));
    }
}

public class AzureSearchServiceTests
{
    [Fact]
    public async Task SearchBySourceReferenceAsync_FiltersSourceTenantAndGroup()
    {
        RecordingAzureSearch sut = new();
        sut.Config.Setup(config => config[GKeys.TenantID]).Returns("7");
        sut.Config.Setup(config => config[GKeys.GroupID]).Returns("2");

        List<TextChunk> result = await sut.SearchBySourceReferenceAsync("fs", CancellationToken.None);

        Assert.Equal("SourceReference eq 'fs' and TenantID eq '7' and GroupID eq '2'", Assert.Single(sut.Filters));
        Assert.Equal("1", result[0].ID);
    }

    [Fact]
    public async Task GetDocumentReferencesByPathAsync_FiltersPathTenantAndGroup()
    {
        RecordingAzureSearch sut = new();

        await sut.GetDocumentReferencesByPathAsync("/docs", CancellationToken.None);

        Assert.Equal("Path eq '/docs' and TenantID eq '0' and GroupID eq '0'", Assert.Single(sut.Filters));
    }

    [Fact]
    public async Task DeleteAllAsync_DeletesIdsForThatCompany()
    {
        RecordingAzureSearch sut = new();

        await sut.DeleteAllAsync(42, CancellationToken.None);

        Assert.Equal("TenantID eq '42'", Assert.Single(sut.Filters));
        Assert.Equal(["1", "2"], Assert.Single(sut.Deleted));
    }

    [Fact]
    public async Task DeleteAllAsync_WhenIndexIsMissing_DoesNothing()
    {
        RecordingAzureSearch sut = new() { IndexExists = false };

        await sut.DeleteAllAsync(1, CancellationToken.None);

        Assert.Empty(sut.Filters);
        Assert.Empty(sut.Deleted);
    }

    [Fact]
    public async Task UploadDocumentsAsync_WritesScopeFieldsFromConfiguration()
    {
        RecordingAzureSearch sut = new();
        sut.Config.Setup(config => config[GKeys.TenantID]).Returns("4");
        sut.Config.Setup(config => config[GKeys.GroupID]).Returns("8");
        sut.Config.Setup(config => config[GKeys.Path]).Returns("/onedrive");

        await sut.UploadDocumentsAsync(
        [
            new TextChunk
            {
                ID = "1",
                Content = "body",
                DocumentReference = "fs@a.txt",
                SourceReference = "fs",
                Embedding = new float[] { 0.5f }
            }
        ], CancellationToken.None);

        SearchDocument document = Assert.Single(sut.Uploaded);
        Assert.Equal("fs", document[AzureSearchSchema.SourceReference]);
        Assert.Equal("4", document[AzureSearchSchema.TenantId]);
        Assert.Equal("8", document[AzureSearchSchema.GroupId]);
        Assert.Equal("/onedrive", document[AzureSearchSchema.Path]);
    }

    sealed class RecordingAzureSearch : AzureSearchService
    {
        public Mock<IDynamicConfiguration> Config { get; }
        public List<string> Filters { get; } = [];
        public List<SearchDocument> Uploaded { get; } = [];
        public List<List<string>> Deleted { get; } = [];
        public bool IndexExists { get; set; } = true;

        public RecordingAzureSearch()
            : this(new Mock<IDynamicConfiguration>())
        {
        }

        RecordingAzureSearch(Mock<IDynamicConfiguration> config)
            : base(config.Object, Mock.Of<ILogger<AzureSearchService>>(), Options.Create(new Generellem.DependencyInjection.GenerellemOptions()))
        {
            Config = config;
        }

        protected override Task<bool> DoesIndexExistAsync(CancellationToken cancellationToken) =>
            Task.FromResult(IndexExists);

        protected override Task EnsureFilterFieldsAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        protected override Task<List<TextChunk>> QueryByFilterAsync(string filter, CancellationToken cancellationToken)
        {
            Filters.Add(filter);
            return Task.FromResult<List<TextChunk>>([new() { ID = "1", DocumentReference = "fs@a.txt", SourceReference = "fs" }]);
        }

        protected override Task<List<string>> QueryIdsByFilterAsync(string filter, CancellationToken cancellationToken)
        {
            Filters.Add(filter);
            return Task.FromResult<List<string>>(["1", "2"]);
        }

        public override Task DeleteDocumentReferencesAsync(List<string> idsToDelete, CancellationToken cancellationToken)
        {
            Deleted.Add(idsToDelete);
            return Task.CompletedTask;
        }

        protected override Task IndexDocumentsAsync(IReadOnlyList<SearchDocument> documents, CancellationToken cancellationToken)
        {
            Uploaded.AddRange(documents);
            return Task.CompletedTask;
        }
    }
}