using System.Net;
using System.Text;
using System.Text.Json;

using Generellem.DependencyInjection;
using Generellem.Document.DocumentTypes;
using Generellem.Embedding.Ollama;
using Generellem.Processors;
using Generellem.Rag;
using Generellem.Services;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Generellem.Embedding.Tests;

public class OllamaEmbeddingBatchTests
{
    [Fact]
    public async Task EmbedAsync_SendsChunksInBatches()
    {
        RecordingHandler handler = new();
        OllamaEmbedding sut = CreateSut(handler, batchSize: 2);

        List<TextChunk> chunks = await sut.EmbedAsync(
            "Test document text",
            Mock.Of<IDocumentType>(),
            "FileSystem@doc.txt",
            new Progress<IngestionProgress>(),
            CancellationToken.None);

        Assert.Equal(3, chunks.Count);
        Assert.Equal(2, handler.Calls);
        Assert.Equal([2, 1], handler.InputCounts);
        Assert.All(handler.Paths, path => Assert.EndsWith("api/embed", path));
        Assert.All(chunks, chunk => Assert.Equal(4, chunk.Embedding.Length));
    }

    [Fact]
    public async Task GetEmbeddingAsync_UsesTheEmbedEndpoint()
    {
        RecordingHandler handler = new();
        OllamaEmbedding sut = CreateSut(handler, batchSize: 16);

        ReadOnlyMemory<float> vector = await sut.GetEmbeddingAsync("hello", CancellationToken.None);

        Assert.Equal(4, vector.Length);
        Assert.Equal(1, handler.Calls);
        Assert.Equal(1, Assert.Single(handler.InputCounts));
        Assert.EndsWith("api/embed", Assert.Single(handler.Paths));
    }

    static OllamaEmbedding CreateSut(RecordingHandler handler, int batchSize)
    {
        Mock<IGenerellemHttpClientFactory> factory = new();
        factory.Setup(http => http.Create()).Returns(() => new HttpClient(handler, disposeHandler: false));

        GenerellemOptions options = new()
        {
            EmbeddingBatchSize = batchSize,
            OllamaEndpoint = "http://localhost:11434",
            OllamaEmbeddingModel = "nomic-embed-text",
            ChunkSize = 9,
            ChunkOverlap = 0
        };

        return new OllamaEmbedding(
            factory.Object,
            Options.Create(options),
            Mock.Of<ILogger<OllamaEmbedding>>(),
            new TextProcessor(Options.Create(options)));
    }

    sealed class RecordingHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }

        public List<int> InputCounts { get; } = [];

        public List<string> Paths { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Paths.Add(request.RequestUri?.ToString() ?? string.Empty);
            string body = await request.Content!.ReadAsStringAsync(cancellationToken);
            using JsonDocument document = JsonDocument.Parse(body);
            int count = document.RootElement.GetProperty("input").GetArrayLength();
            InputCounts.Add(count);

            float[][] embeddings = Enumerable.Range(0, count)
                .Select(_ => new[] { 0.1f, 0.2f, 0.3f, 0.4f })
                .ToArray();
            string json = JsonSerializer.Serialize(new { embeddings });

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }
    }
}
