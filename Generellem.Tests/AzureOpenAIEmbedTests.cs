using Azure;
using Azure.AI.OpenAI;

using Generellem.DependencyInjection;
using Generellem.Document.DocumentTypes;
using Generellem.Embedding;
using Generellem.Embedding.AzureOpenAI;
using Generellem.Llm;
using Generellem.Processors;
using Generellem.Rag;
using Generellem.Services;
using Generellem.Tests;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpenAI.Embeddings;

using Polly;

using System.ClientModel;

namespace Generellem.Rag.Tests;

public class AzureOpenAIEmbedTests
{
    const string DefaultDocumentReference = "source@file";

    readonly Mock<ISearchService> azSearchSvcMock = new();
    readonly Mock<IDynamicConfiguration> configMock = new();
    readonly Mock<IDocumentType> docTypeMock = new();
    readonly Mock<ILogger<AzureOpenAIEmbedding>> logMock = new();
    readonly Mock<IProgress<IngestionProgress>> progMock = new();
    readonly Mock<AzureOpenAIClient> openAiClientMock = new();
    readonly Mock<EmbeddingClient> embeddingClientMock = new();
    readonly Mock<ClientResultMock<OpenAIEmbedding>> embeddingResultMock = new();
    readonly Mock<LlmClientFactory> llmClientFactMock = new();

    OpenAIEmbedding embedding = OpenAIEmbeddingsModelFactory.OpenAIEmbedding(0, TestEmbeddings.CreateEmbeddingArray());
    readonly List<List<string>> embeddingBatches = [];
    readonly IEmbedding azureOpenAiEmbedding;

    public AzureOpenAIEmbedTests()
    {
        docTypeMock
            .Setup(doc => doc.GetTextAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync("text content");

        configMock
            .Setup(config => config[GKeys.AzOpenAIEndpointName])
            .Returns("https://generellem");
        configMock
            .Setup(config => config[GKeys.AzOpenAIEmbeddingName])
            .Returns("generellem-embedding");
        configMock
            .Setup(config => config[GKeys.AzOpenAIApiKey])
            .Returns("generellem-key");
        llmClientFactMock = new(configMock.Object);

        openAiClientMock.Setup(client => client.GetEmbeddingClient(It.IsAny<string>()))
            .Returns(embeddingClientMock.Object);
        embeddingClientMock
            .Setup(embed => embed.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embeddingResultMock.Object);
        embeddingClientMock
            .Setup(embed => embed.GenerateEmbeddingsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<string> inputs, EmbeddingGenerationOptions _, CancellationToken _) =>
                Task.FromResult(EmbeddingResult(inputs)));
        embeddingResultMock
            .SetupGet(embed => embed.Value)
            .Returns(embedding);

        llmClientFactMock.Setup(llm => llm.CreateOpenAIClient()).Returns(openAiClientMock.Object);
        llmClientFactMock.Setup(llm => llm.CreateEmbeddingClient()).Returns(embeddingClientMock.Object);

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
        azSearchSvcMock
            .Setup(srchSvc => srchSvc.GetDocumentReferencesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(chunks);
        azSearchSvcMock
            .Setup(srchSvc => srchSvc.SearchAsync(It.IsAny<ReadOnlyMemory<float>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(chunks);

        azureOpenAiEmbedding = CreateSut();
    }

    static TextProcessor Processor(int chunkSize, int overlap) =>
        new(Options.Create(new GenerellemOptions { ChunkSize = chunkSize, ChunkOverlap = overlap }));

    AzureOpenAIEmbedding CreateSut(int chunkSize = 5000, int overlap = 100, int? batchSize = null)
    {
        IOptions<GenerellemOptions>? embeddingOptions = batchSize is int size
            ? Options.Create(new GenerellemOptions { EmbeddingBatchSize = size })
            : null;

        return new(llmClientFactMock.Object, logMock.Object, Processor(chunkSize, overlap), embeddingOptions);
    }

    ClientResult<OpenAIEmbeddingCollection> EmbeddingResult(IEnumerable<string> inputs, IReadOnlyList<float[]>? vectors = null)
    {
        List<string> texts = inputs.ToList();
        embeddingBatches.Add(texts);

        List<OpenAIEmbedding> embeddings = [];
        for (int i = 0; i < texts.Count; i++)
        {
            float[] vector = vectors is null ? TestEmbeddings.CreateEmbeddingArray() : vectors[i];
            embeddings.Add(OpenAIEmbeddingsModelFactory.OpenAIEmbedding(i, vector));
        }

        OpenAIEmbeddingCollection collection = OpenAIEmbeddingsModelFactory.OpenAIEmbeddingCollection(
            embeddings,
            "test",
            OpenAIEmbeddingsModelFactory.EmbeddingTokenUsage(texts.Count, texts.Count));

        return new ClientResultMock<OpenAIEmbeddingCollection>(collection, new PipeLineResponseMock());
    }

    IReadOnlyList<string> EmbeddedTexts() => embeddingBatches.SelectMany(batch => batch).ToList();

    [Fact]
    public async Task EmbedAsync_CallsGenerateEmbeddingAsync()
    {
        await azureOpenAiEmbedding.EmbedAsync("Test document text", docTypeMock.Object, DefaultDocumentReference, progMock.Object, CancellationToken.None);

        llmClientFactMock.Verify(
            client => client.CreateEmbeddingClient(),
            Times.Once);
        embeddingClientMock.Verify(
            client => client.GenerateEmbeddingsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetEmbeddingAsync_CallsGenerateEmbeddingAsync()
    {
        ReadOnlyMemory<float> vector = await azureOpenAiEmbedding.GetEmbeddingAsync("hello", CancellationToken.None);

        Assert.Equal(TestEmbeddings.CreateEmbeddingArray(), vector.ToArray());
        embeddingClientMock.Verify(
            client => client.GenerateEmbeddingAsync("hello", It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EmbedAsync_UsesFewerRequestsThanChunks()
    {
        AzureOpenAIEmbedding sut = CreateSut(chunkSize: 9, overlap: 0, batchSize: 2);

        await sut.EmbedAsync("Test document text", docTypeMock.Object, DefaultDocumentReference, progMock.Object, CancellationToken.None);

        Assert.Equal(["Test ", "document ", "text"], EmbeddedTexts());
        Assert.Equal(2, embeddingBatches.Count);
        Assert.Equal(2, embeddingBatches[0].Count);
        Assert.Single(embeddingBatches[1]);
    }

    [Fact]
    public async Task EmbedAsync_AssignsVectorsByReturnedIndex()
    {
        float[] first = [1f, 0f, 0f];
        float[] second = [0f, 1f, 0f];
        float[] third = [0f, 0f, 1f];
        embeddingClientMock
            .Setup(embed => embed.GenerateEmbeddingsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<string> inputs, EmbeddingGenerationOptions _, CancellationToken _) =>
            {
                List<string> texts = inputs.ToList();
                embeddingBatches.Add(texts);
                OpenAIEmbeddingCollection collection = OpenAIEmbeddingsModelFactory.OpenAIEmbeddingCollection(
                    [
                        OpenAIEmbeddingsModelFactory.OpenAIEmbedding(2, third),
                        OpenAIEmbeddingsModelFactory.OpenAIEmbedding(0, first),
                        OpenAIEmbeddingsModelFactory.OpenAIEmbedding(1, second)
                    ],
                    "test",
                    OpenAIEmbeddingsModelFactory.EmbeddingTokenUsage(3, 3));

                ClientResult<OpenAIEmbeddingCollection> result =
                    new ClientResultMock<OpenAIEmbeddingCollection>(collection, new PipeLineResponseMock());
                return Task.FromResult(result);
            });
        AzureOpenAIEmbedding sut = CreateSut(chunkSize: 9, overlap: 0, batchSize: 16);

        List<TextChunk> chunks = await sut.EmbedAsync("Test document text", docTypeMock.Object, DefaultDocumentReference, progMock.Object, CancellationToken.None);

        Assert.Equal(first, chunks[0].Embedding.ToArray());
        Assert.Equal(second, chunks[1].Embedding.ToArray());
        Assert.Equal(third, chunks[2].Embedding.ToArray());
    }

    [Fact]
    public async Task EmbedAsync_WithNoOverlap_SplitsOnWordBoundaries()
    {
        AzureOpenAIEmbedding sut = CreateSut(chunkSize: 9, overlap: 0);

        await sut.EmbedAsync("Test document text", docTypeMock.Object, DefaultDocumentReference, progMock.Object, CancellationToken.None);

        Assert.Equal(["Test ", "document ", "text"], EmbeddedTexts());
    }

    [Fact]
    public async Task EmbedAsync_WithOverlap_KeepsTheNextWordWhole()
    {
        AzureOpenAIEmbedding sut = CreateSut(chunkSize: 9, overlap: 2);

        await sut.EmbedAsync("Test document text", docTypeMock.Object, DefaultDocumentReference, progMock.Object, CancellationToken.None);

        Assert.Equal(["Test ", "document ", "t text"], EmbeddedTexts());
    }

    [Fact]
    public async Task EmbedAsync_OverlapsChunks()
    {
        AzureOpenAIEmbedding sut = CreateSut(chunkSize: 11, overlap: 3);

        await sut.EmbedAsync("Test document text", docTypeMock.Object, DefaultDocumentReference, progMock.Object, CancellationToken.None);

        Assert.Equal(["Test ", "document ", "nt text"], EmbeddedTexts());
    }

    [Fact]
    public async Task EmbedAsync_ReturnsEmbeddedTextChunks()
    {
        TextChunk expectedChunk = new()
        {
            Content = "Test document text",
            Embedding = TestEmbeddings.CreateEmbeddingArray(),
            DocumentReference = DefaultDocumentReference
        };
        docTypeMock
            .Setup(doc => doc.GetTextAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync("Test document text");

        List<TextChunk> textChunks = await azureOpenAiEmbedding.EmbedAsync("Test document text", docTypeMock.Object, DefaultDocumentReference, progMock.Object, CancellationToken.None);

        TextChunk actualChunk = textChunks.First();
        Assert.Equal(expectedChunk.Content, actualChunk.Content);
        Assert.Equal(expectedChunk.DocumentReference, actualChunk.DocumentReference);
        float[] expectedEmbedding = expectedChunk.Embedding.ToArray();
        float[] actualEmbedding = actualChunk.Embedding.ToArray();
        Assert.Equal(expectedEmbedding.Length, actualEmbedding.Length);
        for (int i = 0; i < expectedEmbedding.Length; i++)
            Assert.Equal(expectedEmbedding[i], actualEmbedding[i]);
    }

    [Fact]
    public async Task EmbedAsync_WithRequestFailedException_LogsAnError()
    {
        azureOpenAiEmbedding.Pipeline = new ResiliencePipelineBuilder().Build();
        embeddingClientMock
            .Setup(client => client.GenerateEmbeddingsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()))
            .Throws(new RequestFailedException("Unauthorized"));

        await Assert.ThrowsAsync<RequestFailedException>(async () =>
            await azureOpenAiEmbedding.EmbedAsync("Test document text", docTypeMock.Object, DefaultDocumentReference, progMock.Object, CancellationToken.None));

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
}
