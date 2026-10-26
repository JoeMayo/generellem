using Generellem.DependencyInjection;
using Generellem.Embedding;
using Generellem.Repository;
using Generellem.Services;
using Generellem.Services.Sqlite;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Generellem.Services.Tests;

public class SqliteSearchServiceTests : IDisposable
{
    readonly string tempDir;
    readonly GenerellemContext ctx;
    readonly SqliteSearchService sut;

    public SqliteSearchServiceTests()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "generellem-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        Mock<IGenerellemFiles> files = new();
        files.Setup(f => f.GetAppDataPath(It.IsAny<string>()))
            .Returns<string>(name => Path.Combine(tempDir, string.IsNullOrEmpty(name) ? "generellem.db" : name));

        ctx = new GenerellemContext(files.Object);
        ctx.Database.EnsureCreated();

        GenerellemOptions options = new() { TopK = 2, EmbeddingDimensions = 4 };
        sut = new SqliteSearchService(ctx, Options.Create(options));
    }

    public void Dispose()
    {
        ctx.Dispose();
        try
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    [Fact]
    public async Task UploadAndSearch_PersistsAndRanks()
    {
        await sut.UploadDocumentsAsync(
        [
            new TextChunk
            {
                ID = Guid.NewGuid().ToString(),
                Content = "alpha",
                DocumentReference = "fs@a",
                SourceReference = "fs",
                Embedding = new float[] { 1f, 0f, 0f, 0f }
            },
            new TextChunk
            {
                ID = Guid.NewGuid().ToString(),
                Content = "beta",
                DocumentReference = "fs@b",
                SourceReference = "fs",
                Embedding = new float[] { 0f, 1f, 0f, 0f }
            }
        ], CancellationToken.None);

        // New context against same DB path to prove persistence
        Mock<IGenerellemFiles> files = new();
        files.Setup(f => f.GetAppDataPath(It.IsAny<string>()))
            .Returns<string>(name => Path.Combine(tempDir, string.IsNullOrEmpty(name) ? "generellem.db" : name));

        await using GenerellemContext ctx2 = new(files.Object);
        SqliteSearchService sut2 = new(ctx2, Options.Create(new GenerellemOptions { TopK = 1 }));

        List<TextChunk> results = await sut2.SearchAsync(new float[] { 1f, 0f, 0f, 0f }, CancellationToken.None);

        Assert.Single(results);
        Assert.Equal("alpha", results[0].Content);
    }

    [Fact]
    public async Task DeleteDocumentReferencesAsync_RemovesRows()
    {
        string id = Guid.NewGuid().ToString();
        await sut.UploadDocumentsAsync(
        [
            new TextChunk
            {
                ID = id,
                Content = "x",
                DocumentReference = "fs@a",
                SourceReference = "fs",
                Embedding = new float[] { 1f, 0f, 0f, 0f }
            }
        ], CancellationToken.None);

        await sut.DeleteDocumentReferencesAsync([id], CancellationToken.None);

        List<TextChunk> remaining = await sut.GetDocumentReferenceAsync("fs@a", CancellationToken.None);
        Assert.Empty(remaining);
    }
}
