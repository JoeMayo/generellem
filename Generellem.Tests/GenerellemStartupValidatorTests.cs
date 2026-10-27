using Generellem.DependencyInjection;
using Generellem.Services;

using Microsoft.Extensions.Options;

namespace Generellem.DependencyInjection.Tests;

public class GenerellemStartupValidatorTests
{
    [Fact]
    public async Task StartAsync_InMemoryWithDefaults_DoesNotThrow()
    {
        GenerellemStartupValidator validator = Create(new GenerellemOptions { Profile = GenerellemProfile.InMemory });

        await validator.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_SqliteWithDefaults_DoesNotThrow()
    {
        GenerellemStartupValidator validator = Create(new GenerellemOptions { Profile = GenerellemProfile.Sqlite });

        await validator.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_OllamaWithDefaults_DoesNotThrow()
    {
        GenerellemStartupValidator validator = Create(new GenerellemOptions { Profile = GenerellemProfile.Ollama });

        await validator.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_OllamaWithBlankEndpoint_Throws()
    {
        GenerellemOptions options = new()
        {
            Profile = GenerellemProfile.Ollama,
            OllamaEndpoint = " "
        };
        GenerellemStartupValidator validator = Create(options);

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() => validator.StartAsync(CancellationToken.None));

        Assert.Contains(nameof(GenerellemOptions.OllamaEndpoint), ex.Message);
    }

    [Fact]
    public async Task StartAsync_InMemoryWithInvalidChunkOverlap_Throws()
    {
        GenerellemOptions options = new()
        {
            Profile = GenerellemProfile.InMemory,
            ChunkSize = 100,
            ChunkOverlap = 100
        };
        GenerellemStartupValidator validator = Create(options);

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() => validator.StartAsync(CancellationToken.None));

        Assert.Contains(nameof(GenerellemOptions.ChunkOverlap), ex.Message);
    }

    [Fact]
    public async Task StartAsync_InvalidEmbeddingBatchSize_Throws()
    {
        GenerellemOptions options = new()
        {
            Profile = GenerellemProfile.InMemory,
            EmbeddingBatchSize = 0
        };
        GenerellemStartupValidator validator = Create(options);

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() => validator.StartAsync(CancellationToken.None));

        Assert.Contains(nameof(GenerellemOptions.EmbeddingBatchSize), ex.Message);
    }

    [Fact]
    public async Task StartAsync_AzureWithoutKeys_NamesEveryRequiredKey()
    {
        GenerellemStartupValidator validator = Create(new GenerellemOptions { Profile = GenerellemProfile.Azure });

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() => validator.StartAsync(CancellationToken.None));

        string[] required =
        [
            GKeys.AzOpenAIApiKey,
            GKeys.AzOpenAIDeploymentName,
            GKeys.AzOpenAIEmbeddingName,
            GKeys.AzOpenAIEndpointName,
            GKeys.QdrantApiKey,
            GKeys.QdrantCollection,
            GKeys.QdrantEndpoint
        ];

        foreach (string key in required)
            Assert.Contains(key, ex.Message);

        Assert.DoesNotContain(GKeys.AzSearchServiceAdminApiKey, ex.Message);
        Assert.DoesNotContain(GKeys.AzBlobConnectionString, ex.Message);
        Assert.DoesNotContain(GKeys.CosmosConnectionString, ex.Message);
        Assert.DoesNotContain(GKeys.MSGraphClientID, ex.Message);
    }

    [Fact]
    public async Task StartAsync_AzureSearchWithoutSearchKeys_NamesSearchKeys()
    {
        GenerellemStartupValidator validator = Create(new GenerellemOptions
        {
            Profile = GenerellemProfile.Azure,
            UseAzureSearch = true
        });

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() => validator.StartAsync(CancellationToken.None));

        Assert.Contains(GKeys.AzSearchServiceAdminApiKey, ex.Message);
        Assert.Contains(GKeys.AzSearchServiceEndpoint, ex.Message);
        Assert.Contains(GKeys.AzSearchServiceIndex, ex.Message);
        Assert.DoesNotContain(GKeys.QdrantEndpoint, ex.Message);
    }

    [Fact]
    public async Task StartAsync_AzureWithRequiredKeys_DoesNotThrow()
    {
        Dictionary<string, string?> values = new()
        {
            [GKeys.AzOpenAIApiKey] = "key",
            [GKeys.AzOpenAIDeploymentName] = "chat",
            [GKeys.AzOpenAIEmbeddingName] = "embed",
            [GKeys.AzOpenAIEndpointName] = "https://example.openai.azure.com",
            [GKeys.QdrantApiKey] = "qdrant-key",
            [GKeys.QdrantCollection] = "generellem",
            [GKeys.QdrantEndpoint] = "http://localhost:6334"
        };
        GenerellemStartupValidator validator = Create(new GenerellemOptions { Profile = GenerellemProfile.Azure }, values);

        await validator.StartAsync(CancellationToken.None);
    }

    static GenerellemStartupValidator Create(GenerellemOptions options, Dictionary<string, string?>? values = null) =>
        new(Options.Create(options), new MemoryConfiguration(values));

    sealed class MemoryConfiguration(Dictionary<string, string?>? values = null) : IDynamicConfiguration
    {
        readonly Dictionary<string, string?> values = values ?? [];

        public string? this[string index]
        {
            get => values.TryGetValue(index, out string? value) ? value : null;
            set => values[index] = value;
        }
    }
}