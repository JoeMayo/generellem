using Azure.AI.OpenAI;

using Generellem.Document.DocumentTypes;
using Generellem.DocumentSource;
using Generellem.Embedding;
using Generellem.Llm;
using Generellem.Rag;
using Generellem.Repository;
using Generellem.Services;
using Generellem.Tests;

using Microsoft.Extensions.Logging;

using OpenAI.Chat;

using System.Text;

namespace Generellem.Processors.Tests;

public class IngestionTests
{
    const string DefaultDocumentText = "Test document text";
    const string DefaultFilePath = "TestDocs\\file.txt";
    const string SpecDescription = "Test Spec Description";

    readonly string DocSource = $"{Environment.MachineName}:{nameof(FileSystem)}";

    readonly Mock<ISearchService> searchSvcMock = new();
    readonly Mock<IDocumentHashRepository> docHashRepMock = new();
    readonly Mock<IDocumentSource> docSourceMock = new();
    readonly Mock<IDocumentSourceFactory> docSourceFactoryMock = new();
    readonly Mock<IDocumentType> docTypeMock = new();
    readonly Mock<IDynamicConfiguration> configMock = new();
    readonly Mock<IEmbedding> embedMock = new();
    readonly Mock<ILogger<Ingestion>> logMock = new();
    readonly Mock<IRag> ragMock = new();

    readonly Mock<AzureOpenAIClient> openAIClientMock = new();

    readonly Mock<LlmClientFactory> llmClientFactMock;

    readonly IGenerellemIngestion ingestion;

    readonly Queue<ChatMessage> chatHistory = new();
    readonly List<IDocumentSource> docSources = [];

    readonly ReadOnlyMemory<float> embedding;

    public IngestionTests()
    {
        docSources.Add(docSourceMock.Object);

        docSourceFactoryMock
            .Setup(fact => fact.GetDocumentSources())
            .Returns(docSources);

        embedding = new ReadOnlyMemory<float>(TestEmbeddings.CreateEmbeddingArray());
        embedMock
            .Setup(e => e.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);

        List<TextChunk> chunks =
        [
            new()
            {
                ID = "id1",
                Content = "chunk1",
                DocumentReference = "documentReference1"
            },
            new()
            {
                ID = "id2",
                Content = "chunk2",
                DocumentReference = "documentReference2"
            }
        ];
        searchSvcMock
            .Setup(srchSvc => srchSvc.GetDocumentReferencesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(chunks);
        searchSvcMock
            .Setup(srchSvc => srchSvc.SearchAsync(It.IsAny<ReadOnlyMemory<float>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(chunks);

        embedMock
            .Setup(embed => embed.EmbedAsync(It.IsAny<string>(), It.IsAny<IDocumentType>(), It.IsAny<string>(), It.IsAny<IProgress<IngestionProgress>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(chunks);

        configMock
            .Setup(config => config[GKeys.AzOpenAIEndpointName])
            .Returns("https://generellem");
        configMock
            .Setup(config => config[GKeys.AzOpenAIApiKey])
            .Returns("generellem-key");
        llmClientFactMock = new(configMock.Object);

        llmClientFactMock.Setup(llm => llm.CreateOpenAIClient()).Returns(openAIClientMock.Object);

        ingestion = new Ingestion(
            docHashRepMock.Object,
            docSourceFactoryMock.Object,
            embedMock.Object,
            logMock.Object,
            searchSvcMock.Object);
    }

    void SetupGetDocumentsAsync(string filePath)
    {
        async IAsyncEnumerable<DocumentInfo> GetDocInfos()
        {
            MemoryStream memStream = new(Encoding.Default.GetBytes("test"));
            yield return new DocumentInfo(DocSource, memStream, new Text(), filePath, SpecDescription);
            await Task.CompletedTask;
        }
        docSourceMock.Setup(docSrc => docSrc.GetDocumentsAsync(It.IsAny<CancellationToken>())).Returns(GetDocInfos);
    }

    [Fact]
    public async Task InsertOrUpdateDocumentAsync_CallsUploadDocuments()
    {
        MemoryStream memStream = new(Encoding.Default.GetBytes(DefaultDocumentText));
        DocumentInfo docInfo = new(DocSource, memStream, new Text(), DefaultFilePath, SpecDescription);
        IProgress<IngestionProgress> progress = new Progress<IngestionProgress>();

        await ingestion.InsertOrUpdateDocumentAsync(docInfo, DefaultDocumentText, docSourceMock.Object, progress, CancellationToken.None);

        searchSvcMock.Verify(srchSvc =>
            srchSvc.UploadDocumentsAsync(It.IsAny<List<TextChunk>>(), It.IsAny<CancellationToken>()),
            Times.Once());
    }

    [Fact]
    public async Task InsertOrUpdateDocumentAsync_WithEmptyChunks_DoesNotCallUploadDocuments()
    {
        List<TextChunk> chunks =
        [
            new()
            {
                Content = DefaultDocumentText,
                Embedding = TestEmbeddings.CreateEmbeddingArray(),
                DocumentReference = "file"
            }
        ];        
        MemoryStream memStream = new(Encoding.Default.GetBytes(string.Empty));
        DocumentInfo docInfo = new(DocSource, memStream, new Text(), DefaultFilePath, SpecDescription);
        IProgress<IngestionProgress> progress = new Progress<IngestionProgress>();

        await ingestion.InsertOrUpdateDocumentAsync(docInfo, DefaultDocumentText, docSourceMock.Object, progress, CancellationToken.None);

        searchSvcMock.Verify(srchSvc =>
            srchSvc.UploadDocumentsAsync(chunks, It.IsAny<CancellationToken>()),
            Times.Never());
    }
    
    [Fact]
    public async Task ProcessFilesAsync_CallsGetDocumentsAsync()
    {
        SetupGetDocumentsAsync("TestDocs\\file.txt");

        await ingestion.IngestDocumentsAsync(new Progress<IngestionProgress>(), CancellationToken.None);

        docSourceMock.Verify(docSrc => docSrc.GetDocumentsAsync(It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task ProcessFilesAsync_CallsGetTextAsync()
    {
        SetupGetDocumentsAsync("TestDocs\\file.txt");
        docTypeMock
            .Setup(docType => docType.GetTextAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync("Sample Text");
        async IAsyncEnumerable<DocumentInfo> GetDocInfos()
        {
            yield return new DocumentInfo(DocSource, Mock.Of<MemoryStream>(), docTypeMock.Object, "file.txt", SpecDescription);
            await Task.CompletedTask;
        }
        docSourceMock.Setup(docSrc => docSrc.GetDocumentsAsync(It.IsAny<CancellationToken>())).Returns(GetDocInfos);

        await ingestion.IngestDocumentsAsync(new Progress<IngestionProgress>(), CancellationToken.None);

        docTypeMock.Verify(
            docType => docType.GetTextAsync(It.IsAny<Stream>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessFilesAsync_GetTextAsyncThrows_LogsWarning()
    {
        SetupGetDocumentsAsync("TestDocs\\file.txt");
        docTypeMock
            .Setup(docType => docType.GetTextAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception());
        async IAsyncEnumerable<DocumentInfo> GetDocInfos()
        {
            yield return new DocumentInfo(DocSource, Mock.Of<MemoryStream>(), docTypeMock.Object, "file.txt", SpecDescription);
            await Task.CompletedTask;
        }
        docSourceMock.Setup(docSrc => docSrc.GetDocumentsAsync(It.IsAny<CancellationToken>())).Returns(GetDocInfos);

        await ingestion.IngestDocumentsAsync(new Progress<IngestionProgress>(), CancellationToken.None);

        logMock
            .Verify(
                l => l.Log(
                    LogLevel.Warning,
                    GenerellemLogEvents.DocumentError,
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()),
                Times.Once);
    }

    [Fact]
    public async Task ProcessFilesAsync_ProcessesSupportedDocument()
    {
        SetupGetDocumentsAsync("TestDocs\\file.txt");

        await ingestion.IngestDocumentsAsync(new Progress<IngestionProgress>(), CancellationToken.None);

        embedMock.Verify(
            embed => embed.EmbedAsync(It.IsAny<string>(), It.IsAny<IDocumentType>(), It.IsAny<string>(), It.IsAny<IProgress<IngestionProgress>>(), CancellationToken.None),
            Times.Once());
    }

    [Fact]
    public async Task ProcessFilesAsync_SkipsUnsupportedDocument()
    {
        SetupGetDocumentsAsync("TestDocs\\file.xyz");
        async IAsyncEnumerable<DocumentInfo> GetDocInfos()
        {
            yield return new DocumentInfo(DocSource, new MemoryStream(), new Unknown(), "file.xyz", SpecDescription);
            await Task.CompletedTask;
        }
        docSourceMock.Setup(docSrc => docSrc.GetDocumentsAsync(It.IsAny<CancellationToken>())).Returns(GetDocInfos);

        await ingestion.IngestDocumentsAsync(new Progress<IngestionProgress>(), CancellationToken.None);

        embedMock.Verify(
            embed => embed.EmbedAsync(It.IsAny<string>(), It.IsAny<IDocumentType>(), It.IsAny<string>(), It.IsAny<Progress<IngestionProgress>>(), CancellationToken.None),
            Times.Never);
    }

    [Fact]
    public async Task ProcessFilesAsync_WithNewDocument_InsertsHash()
    {
        SetupGetDocumentsAsync("TestDocs\\file.txt");
        docHashRepMock
            .Setup(docHashRep => docHashRep.GetDocumentHashAsync(It.IsAny<string>()))
            .ReturnsAsync((DocumentHash?)null);

        await ingestion.IngestDocumentsAsync(new Progress<IngestionProgress>(), CancellationToken.None);

        docHashRepMock.Verify(
            docHashRep => docHashRep.InsertAsync(It.IsAny<DocumentHash>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessFilesAsync_WithChangedDocument_UpdatesHash()
    {
        SetupGetDocumentsAsync("TestDocs\\file.txt");
        docHashRepMock
            .Setup(docHashRep => docHashRep.GetDocumentHashAsync(It.IsAny<string>()))
            .ReturnsAsync(new DocumentHash { DocumentReference = "", Hash = Guid.NewGuid().ToString() });

        await ingestion.IngestDocumentsAsync(new Progress<IngestionProgress>(), CancellationToken.None);

        docHashRepMock.Verify(
            docHashRep => docHashRep.UpdateAsync(It.IsAny<DocumentHash>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessFilesAsync_WithUnchangedDocument_DoesNotProcess()
    {
        // DocumentInfo from SetupGetDocumentsAsync has an empty MemoryStream
        // that returns "test" that hashes to SHA256BlankStringHash.
        const string SHA256BlankStringHash = "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08";
        SetupGetDocumentsAsync("TestDocs\\file.txt");
        docHashRepMock
            .Setup(docHashRep => docHashRep.GetDocumentHashAsync(It.IsAny<string>()))
            .ReturnsAsync(new DocumentHash { DocumentReference = "", Hash = SHA256BlankStringHash });

        await ingestion.IngestDocumentsAsync(new Progress<IngestionProgress>(), CancellationToken.None);

        docHashRepMock.Verify(
            docHashRep => docHashRep.UpdateAsync(It.IsAny<DocumentHash>(), It.IsAny<string>()),
            Times.Never);
        docHashRepMock.Verify(
            docHashRep => docHashRep.InsertAsync(It.IsAny<DocumentHash>()),
            Times.Never);
        embedMock.Verify(
            embed => embed.EmbedAsync(It.IsAny<string>(), It.IsAny<IDocumentType>(), It.IsAny<string>(), It.IsAny<Progress<IngestionProgress>>(), CancellationToken.None),
            Times.Never);
    }

    [Fact]
    public async Task IsDocUnchangedAsync_DocumentHashIsNull_InsertsNewHash()
    {
        DocumentInfo doc = new("", null, null, "", "") { DocumentReference = "doc2" };
        string fullText = "new document text";
        docHashRepMock.Setup(repo => repo.GetDocumentHashAsync(doc.DocumentReference))
                      .ReturnsAsync(new DocumentHash());
        Ingestion ingestion = new(docHashRepMock.Object, docSourceFactoryMock.Object, embedMock.Object, logMock.Object, searchSvcMock.Object);

        bool result = await ingestion.ShouldInsertOrUpdateAsync(doc, fullText);

        Assert.True(result);
        docHashRepMock.Verify(repo => repo.InsertAsync(It.IsAny<DocumentHash>()), Times.Once);
    }

    [Fact]
    public async Task IsDocUnchangedAsync_DocumentHashIsDifferent_UpdatesHash()
    {
        DocumentInfo doc = new("", null, null, "", "") { DocumentReference = "doc3" };
        string fullText = "updated document text";
        docHashRepMock.Setup(repo => repo.GetDocumentHashAsync(doc.DocumentReference))
                      .ReturnsAsync(new DocumentHash { DocumentReference = doc.DocumentReference, Hash = "oldHash" });
        Ingestion ingestion = new(docHashRepMock.Object, docSourceFactoryMock.Object, embedMock.Object, logMock.Object, searchSvcMock.Object);

        bool result = await ingestion.ShouldInsertOrUpdateAsync(doc, fullText);

        Assert.True(result);
        docHashRepMock.Verify(repo => repo.UpdateAsync(It.IsAny<DocumentHash>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task IsDocUnchangedAsync_DocumentHashIsSame_ReturnsTrue()
    {
        DocumentInfo doc = new("", null, null, "", "") { DocumentReference = "doc4" };
        string fullText = "same document text";
        Ingestion ingestion = new(docHashRepMock.Object, docSourceFactoryMock.Object, embedMock.Object, logMock.Object, searchSvcMock.Object);
        string hash = Ingestion.ComputeSha256Hash(fullText);
        docHashRepMock.Setup(repo => repo.GetDocumentHashAsync(doc.DocumentReference))
                      .ReturnsAsync(new DocumentHash { DocumentReference = doc.DocumentReference, Hash = hash });

        bool result = await ingestion.ShouldInsertOrUpdateAsync(doc, fullText);

        Assert.False(result);
        docHashRepMock.Verify(repo => repo.UpdateAsync(It.IsAny<DocumentHash>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task IsDocUnchangedAsync_InsertAsyncThrowsException_LogsError()
    {
        DocumentInfo doc = new DocumentInfo("prefix", null, null, null, null) { DocumentReference = "doc1" };
        string fullText = "some text";
        string newHash = Ingestion.ComputeSha256Hash(fullText);

        docHashRepMock.Setup(repo => repo.GetDocumentHashAsync(doc.DocumentReference))
                      .ReturnsAsync((DocumentHash?)null);
        docHashRepMock.Setup(repo => repo.InsertAsync(It.IsAny<DocumentHash>()))
                      .ThrowsAsync(new Exception("InsertAsync exception"));

        Ingestion ingestion = new Ingestion(docHashRepMock.Object, docSourceFactoryMock.Object, embedMock.Object, logMock.Object, searchSvcMock.Object);

        bool result = await ingestion.ShouldInsertOrUpdateAsync(doc, fullText);

        Assert.False(result);
        logMock
            .Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()),
                Times.Once);
    }

    [Fact]
    public async Task IsDocUnchangedAsync_UpdateAsyncThrowsException_LogsError()
    {
        DocumentInfo doc = new DocumentInfo("prefix", null, null, null, null) { DocumentReference = "doc1" };
        string fullText = "some updated text";
        string newHash = Ingestion.ComputeSha256Hash(fullText);
        DocumentHash documentHash = new DocumentHash { DocumentReference = doc.DocumentReference, Hash = "oldHash" };

        docHashRepMock.Setup(repo => repo.GetDocumentHashAsync(doc.DocumentReference))
                      .ReturnsAsync(documentHash);
        docHashRepMock.Setup(repo => repo.UpdateAsync(documentHash, newHash))
                      .ThrowsAsync(new Exception("UpdateAsync exception"));

        Ingestion ingestion = new Ingestion(docHashRepMock.Object, docSourceFactoryMock.Object, embedMock.Object, logMock.Object, searchSvcMock.Object);

        bool result = await ingestion.ShouldInsertOrUpdateAsync(doc, fullText);

        Assert.False(result);
        logMock
            .Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()),
                Times.Once);
    }

    [Fact]
    public async Task RemoveDeletedFilesAsync_WithNoDeletedFiles_DoesNotDeleteAnything()
    {
        string docSourcePrefix = "prefix";
        List<string> documentReferences = new() { "doc1", "doc2" };
        searchSvcMock
            .Setup(svc => svc.GetDocumentReferencesAsync(docSourcePrefix, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TextChunk>
            {
                new TextChunk { DocumentReference = "doc1" },
                new TextChunk { DocumentReference = "doc2" }
            });
        Ingestion ingestion = new(docHashRepMock.Object, docSourceFactoryMock.Object, embedMock.Object, logMock.Object, searchSvcMock.Object);

        await ingestion.RemoveDeletedDocumentsAsync(docSourcePrefix, documentReferences, docSourceMock.Object, CancellationToken.None);

        searchSvcMock.Verify(svc => svc.DeleteDocumentReferencesAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()), Times.Never);
        docHashRepMock.Verify(repo => repo.DeleteAsync(It.IsAny<List<string>>()), Times.Never);
    }

    [Fact]
    public async Task RemoveDeletedFilesAsync_WithDeletedFiles_DeletesCorrectFiles()
    {
        string docSourcePrefix = "prefix";
        List<string> documentReferences = new() { "doc1" };
        searchSvcMock.Setup(svc => svc.GetDocumentReferencesAsync(docSourcePrefix, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new List<TextChunk>
                       {
                       new TextChunk { DocumentReference = "doc1" },
                       new TextChunk { DocumentReference = "doc2", ID = "chunk2" }
                       });
        Ingestion ingestion = new(docHashRepMock.Object, docSourceFactoryMock.Object, embedMock.Object, logMock.Object, searchSvcMock.Object);

        await ingestion.RemoveDeletedDocumentsAsync(docSourcePrefix, documentReferences, docSourceMock.Object, CancellationToken.None);

        searchSvcMock.Verify(svc => svc.DeleteDocumentReferencesAsync(It.Is<List<string>>(ids => ids.Contains("chunk2")), It.IsAny<CancellationToken>()), Times.Once);
        docHashRepMock.Verify(repo => repo.DeleteAsync(It.Is<List<string>>(refs => refs.Contains("doc2"))), Times.Once);
    }
}
