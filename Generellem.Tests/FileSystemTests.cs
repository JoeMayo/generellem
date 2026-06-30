using Generellem.Services;

namespace Generellem.DocumentSource.Tests;

public class FileSystemTests
{
    readonly Mock<IPathProvider> pathProviderMock = new();
    readonly Mock<IPathProviderFactory> pathProviderFactoryMock = new();

    public FileSystemTests()
    {
        pathProviderFactoryMock
            .Setup(fact => fact.Create(It.IsAny<IDocumentSource>()))
            .Returns(pathProviderMock.Object);
    }

    [Fact]
    public async Task GetFiles_ReturnsFiles()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"generellem-fs-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            string testFile = Path.Combine(tempDir, "sample.txt");
            await File.WriteAllTextAsync(testFile, "test content");

            PathSpec fileSpec = new() { Path = tempDir };
            pathProviderMock
                .Setup(fs => fs.GetPathsAsync(It.IsAny<string>()))
                .ReturnsAsync(new[] { fileSpec });

            var fileSystem = new FileSystem(pathProviderFactoryMock.Object);

            List<DocumentInfo> documents = [];

            await foreach (DocumentInfo docInfo in fileSystem.GetDocumentsAsync(CancellationToken.None))
            {
                Assert.NotEmpty(docInfo.DocumentReference);
                documents.Add(docInfo);
                docInfo.DocStream?.Dispose();
            }

            Assert.Single(documents);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
