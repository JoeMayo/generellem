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
    public async Task StartAsync_ImageVisionWithoutModel_Throws()
    {
        GenerellemStartupValidator validator = Create(new GenerellemOptions
        {
            Profile = GenerellemProfile.InMemory,
            EnableImageVision = true
        });

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => validator.StartAsync(CancellationToken.None));

        Assert.Contains("VisionModel", ex.Message);
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
    public async Task StartAsync_MistralCompatibleEndpoints_DoesNotThrow()
    {
        GenerellemStartupValidator validator = Create(new GenerellemOptions
        {
            Profile = GenerellemProfile.InMemory,
            ChatEndpoint = "https://api.mistral.ai/v1",
            ChatApiKey = "key",
            ChatModel = "mistral-small-latest",
            EmbeddingEndpoint = "https://api.mistral.ai/v1",
            EmbeddingApiKey = "key",
            EmbeddingModel = "mistral-embed"
        });

        await validator.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_LoopbackEndpointsWithoutApiKeys_DoesNotThrow()
    {
        GenerellemStartupValidator validator = Create(new GenerellemOptions
        {
            Profile = GenerellemProfile.InMemory,
            ChatEndpoint = "http://127.0.0.1:11434/v1",
            ChatModel = "llama3.2",
            EmbeddingEndpoint = "http://localhost:11434/v1",
            EmbeddingModel = "nomic-embed-text"
        });

        await validator.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_RemoteEndpointsWithoutApiKeys_Throws()
    {
        GenerellemStartupValidator validator = Create(new GenerellemOptions
        {
            Profile = GenerellemProfile.InMemory,
            ChatEndpoint = "https://api.mistral.ai/v1",
            ChatModel = "mistral-small-latest",
            EmbeddingEndpoint = "https://api.mistral.ai/v1",
            EmbeddingModel = "mistral-embed"
        });

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => validator.StartAsync(CancellationToken.None));

        Assert.Contains(nameof(GenerellemOptions.ChatApiKey), ex.Message);
        Assert.Contains(nameof(GenerellemOptions.EmbeddingApiKey), ex.Message);
    }

    [Fact]
    public async Task StartAsync_ChatEndpointWithoutModel_Throws()
    {
        GenerellemStartupValidator validator = Create(new GenerellemOptions
        {
            Profile = GenerellemProfile.InMemory,
            ChatEndpoint = "http://localhost:11434/v1"
        });

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => validator.StartAsync(CancellationToken.None));

        Assert.Contains(nameof(GenerellemOptions.ChatModel), ex.Message);
    }

    [Fact]
    public async Task StartAsync_ChatEndpointNotHttp_Throws()
    {
        GenerellemStartupValidator validator = Create(new GenerellemOptions
        {
            Profile = GenerellemProfile.InMemory,
            ChatEndpoint = "api.mistral.ai",
            ChatModel = "mistral-small-latest",
            ChatApiKey = "key"
        });

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => validator.StartAsync(CancellationToken.None));

        Assert.Contains(nameof(GenerellemOptions.ChatEndpoint), ex.Message);
    }

    [Theory]
    [InlineData(GenerellemProfile.InMemory)]
    [InlineData(GenerellemProfile.Sqlite)]
    [InlineData(GenerellemProfile.Ollama)]
    [InlineData(GenerellemProfile.Azure)]
    public async Task StartAsync_EmptyMcpServers_DoesNotThrow(GenerellemProfile profile)
    {
        GenerellemStartupValidator validator = Create(
            new GenerellemOptions { Profile = profile, McpServers = [] },
            profile == GenerellemProfile.Azure ? AzureKeys() : null);

        await validator.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_ValidStdioMcpServer_DoesNotThrow()
    {
        GenerellemStartupValidator validator = Create(new GenerellemOptions
        {
            Profile = GenerellemProfile.InMemory,
            McpServers = [Stdio("notes")]
        });

        await validator.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_ValidHttpMcpServer_DoesNotThrow()
    {
        GenerellemStartupValidator validator = Create(new GenerellemOptions
        {
            Profile = GenerellemProfile.Sqlite,
            McpServers = [Http("docs")]
        });

        await validator.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_StdioAndHttpMcpServers_DoesNotThrow()
    {
        GenerellemStartupValidator validator = Create(new GenerellemOptions
        {
            Profile = GenerellemProfile.Ollama,
            McpServers = [Stdio("notes"), Http("docs")]
        });

        await validator.StartAsync(CancellationToken.None);
    }

    [Theory]
    [InlineData("")]
    [InlineData("-notes")]
    [InlineData("notes extra")]
    [InlineData("notes.extra")]
    public async Task StartAsync_McpServerBadName_Throws(string name)
    {
        GenerellemStartupValidator validator = Create(new GenerellemOptions
        {
            Profile = GenerellemProfile.InMemory,
            McpServers = [Stdio(name)]
        });

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => validator.StartAsync(CancellationToken.None));

        Assert.Contains(nameof(McpServerOptions.Name), ex.Message);
    }

    [Fact]
    public async Task StartAsync_McpServerNameAtMaxLength_DoesNotThrow()
    {
        GenerellemStartupValidator validator = Create(new GenerellemOptions
        {
            Profile = GenerellemProfile.InMemory,
            McpServers = [Stdio(new string('n', 64))]
        });

        await validator.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_McpServerNameTooLong_Throws()
    {
        GenerellemStartupValidator validator = Create(new GenerellemOptions
        {
            Profile = GenerellemProfile.InMemory,
            McpServers = [Stdio(new string('a', 65))]
        });

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => validator.StartAsync(CancellationToken.None));

        Assert.Contains(nameof(McpServerOptions.Name), ex.Message);
    }

    [Fact]
    public async Task StartAsync_McpServerDuplicateName_Throws()
    {
        GenerellemStartupValidator validator = Create(new GenerellemOptions
        {
            Profile = GenerellemProfile.InMemory,
            McpServers = [Stdio("notes"), Http("notes")]
        });

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => validator.StartAsync(CancellationToken.None));

        Assert.Contains("unique", ex.Message);
        Assert.DoesNotContain("prefix", ex.Message);
    }

    [Fact]
    public async Task StartAsync_McpServerPrefixCollision_Throws()
    {
        GenerellemStartupValidator validator = Create(new GenerellemOptions
        {
            Profile = GenerellemProfile.InMemory,
            McpServers = [Stdio("notes"), Http("notes-extra")]
        });

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => validator.StartAsync(CancellationToken.None));

        Assert.Contains("prefix", ex.Message);
        Assert.Contains("notes", ex.Message);
        Assert.Contains("notes-extra", ex.Message);
    }

    [Fact]
    public async Task StartAsync_McpNamesThatDifferByCase_DoesNotThrow()
    {
        GenerellemStartupValidator validator = Create(new GenerellemOptions
        {
            Profile = GenerellemProfile.InMemory,
            McpServers = [Stdio("Notes"), Http("notes")]
        });

        await validator.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_StdioMissingCommand_Throws()
    {
        McpServerOptions server = Stdio("notes");
        server.Command = " ";
        GenerellemStartupValidator validator = Create(new GenerellemOptions
        {
            Profile = GenerellemProfile.InMemory,
            McpServers = [server]
        });

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => validator.StartAsync(CancellationToken.None));

        Assert.Contains(nameof(McpServerOptions.Command), ex.Message);
    }

    [Fact]
    public async Task StartAsync_HttpMissingUrl_Throws()
    {
        McpServerOptions server = Http("docs");
        server.Url = "";
        GenerellemStartupValidator validator = Create(new GenerellemOptions
        {
            Profile = GenerellemProfile.InMemory,
            McpServers = [server]
        });

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => validator.StartAsync(CancellationToken.None));

        Assert.Contains(nameof(McpServerOptions.Url), ex.Message);
    }

    [Fact]
    public async Task StartAsync_McpServerSetsCommandAndUrl_Throws()
    {
        McpServerOptions server = Stdio("notes");
        server.Url = "https://mcp.example.com/mcp";
        GenerellemStartupValidator validator = Create(new GenerellemOptions
        {
            Profile = GenerellemProfile.InMemory,
            McpServers = [server]
        });

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => validator.StartAsync(CancellationToken.None));

        Assert.Contains("must not set both", ex.Message);
    }

    [Fact]
    public async Task StartAsync_HttpUrlNotAbsolute_Throws()
    {
        McpServerOptions server = Http("docs");
        server.Url = "mcp.example.com";
        GenerellemStartupValidator validator = Create(new GenerellemOptions
        {
            Profile = GenerellemProfile.InMemory,
            McpServers = [server]
        });

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => validator.StartAsync(CancellationToken.None));

        Assert.Contains(nameof(McpServerOptions.Url), ex.Message);
        Assert.Contains("absolute", ex.Message);
    }

    [Fact]
    public async Task StartAsync_McpServerMissingTransport_Throws()
    {
        McpServerOptions server = Stdio("notes");
        server.Transport = McpTransport.Unspecified;
        GenerellemStartupValidator validator = Create(new GenerellemOptions
        {
            Profile = GenerellemProfile.InMemory,
            McpServers = [server]
        });

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => validator.StartAsync(CancellationToken.None));

        Assert.Contains(nameof(McpServerOptions.Transport), ex.Message);
    }

    [Fact]
    public async Task StartAsync_StdioWithHeaders_Throws()
    {
        McpServerOptions server = Stdio("notes");
        server.Headers["Authorization"] = "Bearer placeholder";
        GenerellemStartupValidator validator = Create(new GenerellemOptions
        {
            Profile = GenerellemProfile.InMemory,
            McpServers = [server]
        });

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => validator.StartAsync(CancellationToken.None));

        Assert.Contains(nameof(McpServerOptions.Headers), ex.Message);
        Assert.DoesNotContain("Bearer", ex.Message);
    }

    [Fact]
    public async Task StartAsync_HttpWithArgumentsOrEnvironment_Throws()
    {
        McpServerOptions server = Http("docs");
        server.Arguments.Add("--verbose");
        server.Environment["TOKEN"] = "secret";
        GenerellemStartupValidator validator = Create(new GenerellemOptions
        {
            Profile = GenerellemProfile.InMemory,
            McpServers = [server]
        });

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => validator.StartAsync(CancellationToken.None));

        Assert.Contains(nameof(McpServerOptions.Arguments), ex.Message);
        Assert.Contains(nameof(McpServerOptions.Environment), ex.Message);
        Assert.DoesNotContain("secret", ex.Message);
    }

    [Fact]
    public async Task StartAsync_McpMaxResourcesNotPositive_Throws()
    {
        McpServerOptions server = Stdio("notes");
        server.MaxResources = 0;
        GenerellemStartupValidator validator = Create(new GenerellemOptions
        {
            Profile = GenerellemProfile.InMemory,
            McpServers = [server]
        });

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => validator.StartAsync(CancellationToken.None));

        Assert.Contains(nameof(McpServerOptions.MaxResources), ex.Message);
    }

    [Fact]
    public async Task StartAsync_McpMaxBlobBytesNotPositive_Throws()
    {
        GenerellemStartupValidator validator = Create(new GenerellemOptions
        {
            Profile = GenerellemProfile.InMemory,
            McpMaxBlobBytes = 0
        });

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => validator.StartAsync(CancellationToken.None));

        Assert.Contains(nameof(GenerellemOptions.McpMaxBlobBytes), ex.Message);
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

    static McpServerOptions Stdio(string name) => new()
    {
        Name = name,
        Transport = McpTransport.Stdio,
        Command = "npx",
        Arguments = ["-y", "server"],
        Environment = new Dictionary<string, string> { ["NOTES_ROOT"] = "/tmp" }
    };

    static McpServerOptions Http(string name) => new()
    {
        Name = name,
        Transport = McpTransport.Http,
        Url = "https://mcp.example.com/mcp",
        Headers = new Dictionary<string, string> { ["Authorization"] = "Bearer placeholder" }
    };

    static Dictionary<string, string?> AzureKeys() => new()
    {
        [GKeys.AzOpenAIApiKey] = "key",
        [GKeys.AzOpenAIDeploymentName] = "chat",
        [GKeys.AzOpenAIEmbeddingName] = "embed",
        [GKeys.AzOpenAIEndpointName] = "https://example.openai.azure.com",
        [GKeys.QdrantApiKey] = "qdrant-key",
        [GKeys.QdrantCollection] = "generellem",
        [GKeys.QdrantEndpoint] = "http://localhost:6334"
    };

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