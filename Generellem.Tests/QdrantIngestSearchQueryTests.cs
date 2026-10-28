using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

using Generellem.DependencyInjection;
using Generellem.Document.DocumentTypes;
using Generellem.DocumentSource;
using Generellem.Embedding;
using Generellem.Embedding.Deterministic;
using Generellem.Llm.Local;
using Generellem.Processors;
using Generellem.Rag;
using Generellem.Repository;
using Generellem.Services.Qdrant;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpenAI.Chat;

using Qdrant.Client;

namespace Generellem.Services.Tests;

/// <summary>
/// Ingests one document into a real Qdrant container, searches it, and answers through the local RAG path.
/// Skips when the Docker daemon is not running so the rest of the suite still runs.
/// </summary>
public class QdrantIngestSearchQueryTests
{
    const string ApiKey = "generellem-it-key";
    const string Marker = "periwinkle-beacon-9173";
    const string Image = "qdrant/qdrant:v1.15.5";

    [RequiresDockerFact]
    public async Task IngestSearchQuery_RoundTripsADocument()
    {
        string collection = "it-" + Guid.NewGuid().ToString("N");
        string appData = Path.Combine(Path.GetTempPath(), "generellem-qdrant-" + Guid.NewGuid().ToString("N"));
        const string sourceReference = "qdrant-it";
        const string documentPath = "guide.txt";
        string documentText = $"The {Marker} note explains how Generellem accepts contributions.";

        await using IContainer container = new ContainerBuilder(Image)
            .WithEnvironment("QDRANT__SERVICE__API_KEY", ApiKey)
            .WithPortBinding(6333, true)
            .WithPortBinding(6334, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request =>
                request
                    .ForPort(6333)
                    .ForPath("/readyz")))
            .Build();

        await container.StartAsync();

        ushort grpcPort = container.GetMappedPublicPort(6334);
        string endpoint = $"http://127.0.0.1:{grpcPort}";

        QdrantClient? client = null;
        GenerellemContext? context = null;
        try
        {
            Directory.CreateDirectory(appData);

            IOptions<GenerellemOptions> options = Options.Create(new GenerellemOptions
            {
                EmbeddingDimensions = 64,
                ChunkSize = 500,
                ChunkOverlap = 0,
                TopK = 3,
                Temperature = 0,
                SummarizeUserIntent = true
            });

            Dictionary<string, string?> settings = new()
            {
                [GKeys.QdrantEndpoint] = endpoint,
                [GKeys.QdrantApiKey] = ApiKey,
                [GKeys.QdrantCollection] = collection
            };
            Mock<IDynamicConfiguration> config = new();
            config.Setup(item => item[It.IsAny<string>()])
                .Returns((string key) => settings.TryGetValue(key, out string? value) ? value : null);

            client = QdrantClientFactory.Create(endpoint, ApiKey);
            QdrantService search = new(config.Object, Mock.Of<ILogger<QdrantService>>(), options, client);
            TextProcessor textProcessor = new(options);
            DeterministicEmbeddingService embedding = new(options, textProcessor);
            context = new GenerellemContext(new TempAppData(appData));
            context.Database.Migrate();

            Ingestion ingestion = new(
                new DocumentHashRepository(context),
                new SingleSourceFactory(new SingleTextSource(sourceReference, documentPath, documentText)),
                embedding,
                Mock.Of<ILogger<Ingestion>>(),
                search);

            await ingestion.IngestDocumentsAsync(new Progress<IngestionProgress>(), CancellationToken.None);

            ReadOnlyMemory<float> vector = await embedding.GetEmbeddingAsync(Marker, CancellationToken.None);
            List<TextChunk> hits = await search.SearchAsync(vector, CancellationToken.None);
            Assert.Contains(hits, hit => hit.Content?.Contains(Marker, StringComparison.Ordinal) == true);

            LocalLlmService llm = new();
            GenerellemDefaultRag rag = new(
                search,
                config.Object,
                embedding,
                llm,
                Mock.Of<ILogger<GenerellemDefaultRag>>(),
                options);
            AzureOpenAIQuery query = new(llm, rag);

            string answer = await query.AskAsync(
                $"Where is the {Marker} note?",
                new Queue<ChatMessage>(),
                CancellationToken.None);

            Assert.Contains(Marker, answer, StringComparison.Ordinal);
            Assert.Contains("Sources:", answer, StringComparison.Ordinal);
            Assert.Contains($"{sourceReference}@{documentPath}", answer, StringComparison.Ordinal);
        }
        finally
        {
            context?.Dispose();
            client?.Dispose();
            if (Directory.Exists(appData))
                Directory.Delete(appData, recursive: true);
        }
    }

    sealed class TempAppData(string root) : IGenerellemFiles
    {
        public string GetAppDataPath(string fileName = "")
        {
            Directory.CreateDirectory(root);
            return Path.Combine(root, fileName);
        }
    }

    sealed class SingleSourceFactory(IDocumentSource source) : IDocumentSourceFactory
    {
        public IEnumerable<IDocumentSource> GetDocumentSources() => [source];
    }

    sealed class SingleTextSource(string reference, string path, string text) : IDocumentSource
    {
        public string Description { get; set; } = "Qdrant integration";

        public string Reference { get; set; } = reference;

        public async IAsyncEnumerable<DocumentInfo> GetDocumentsAsync([EnumeratorCancellation] CancellationToken cancelToken)
        {
            MemoryStream stream = new(Encoding.UTF8.GetBytes(text));
            yield return new DocumentInfo(Reference, stream, new Text(), path, "integration");
            await Task.CompletedTask;
        }
    }
}

/// <summary>
/// Skips the test when <c>docker info</c> cannot reach a daemon.
/// </summary>
public sealed class RequiresDockerFactAttribute : FactAttribute
{
    public RequiresDockerFactAttribute()
    {
        if (!IsDockerAvailable())
            Skip = "Docker is not available.";
    }

    static bool IsDockerAvailable()
    {
        try
        {
            using Process process = new()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = "info",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            if (!process.Start())
                return false;

            if (!process.WaitForExit(5000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                }

                return false;
            }

            return process.ExitCode == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
