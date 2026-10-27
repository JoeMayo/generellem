using Generellem.DependencyInjection;
using Generellem.Embedding;
using Generellem.Embedding.Deterministic;
using Generellem.Llm;
using Generellem.Llm.Local;
using Generellem.Services;
using Generellem.Services.InMemory;
using Generellem.Services.Qdrant;
using Generellem.Services.Sqlite;

using Qdrant.Client;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Generellem.DependencyInjection.Tests;

public class AddGenerellemTests
{
    [Fact]
    public void AddGenerellem_InMemory_RegistersLocalStack()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());

        services.AddGenerellem(GenerellemProfile.InMemory);

        using ServiceProvider sp = services.BuildServiceProvider();

        Assert.IsType<InMemorySearchService>(sp.GetRequiredService<ISearchService>());
        Assert.IsType<DeterministicEmbeddingService>(sp.GetRequiredService<IEmbedding>());
        Assert.IsType<LocalLlmService>(sp.GetRequiredService<ILlm>());
        Assert.Equal(GenerellemProfile.InMemory, sp.GetRequiredService<IOptions<GenerellemOptions>>().Value.Profile);
        Assert.Equal(64, sp.GetRequiredService<IOptions<GenerellemOptions>>().Value.EmbeddingDimensions);
        Assert.True(sp.GetRequiredService<IOptions<GenerellemOptions>>().Value.SummarizeUserIntent);
    }

    [Fact]
    public void AddGenerellem_Sqlite_RegistersSqliteSearch()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());

        services.AddGenerellem(GenerellemProfile.Sqlite);

        using ServiceProvider sp = services.BuildServiceProvider();

        Assert.IsType<SqliteSearchService>(sp.GetRequiredService<ISearchService>());
        Assert.IsType<DeterministicEmbeddingService>(sp.GetRequiredService<IEmbedding>());
        Assert.IsType<LocalLlmService>(sp.GetRequiredService<ILlm>());
    }

    [Fact]
    public void AddGenerellem_InMemory_SearchServiceIsSingleton()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());

        services.AddGenerellem(GenerellemProfile.InMemory);

        using ServiceProvider sp = services.BuildServiceProvider();
        ISearchService a = sp.GetRequiredService<ISearchService>();
        ISearchService b = sp.GetRequiredService<ISearchService>();

        Assert.Same(a, b);
    }

    [Fact]
    public void AddGenerellem_BindsSectionCallbackAndEnvironmentOverrides()
    {
        Dictionary<string, string?> values = new()
        {
            ["Generellem:ChunkSize"] = "1200",
            ["Generellem:ChunkOverlap"] = "50",
            ["Generellem:TopK"] = "7",
            ["Generellem:SummarizeUserIntent"] = "false",
            ["Generellem:EmbeddingBatchSize"] = "8",
            ["Generellem:RunMode"] = "Query",
            ["Generellem:Temperature"] = "0.4",
            ["Generellem:SubFolder"] = "FromJson",
            ["Generellem:OllamaChatModel"] = "from-json",
            ["Generellem:Profile"] = "Azure",
            [GKeys.OllamaChatModel] = "from-env"
        };

        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new MapConfiguration(values));
        services.AddGenerellem(GenerellemProfile.InMemory, options => options.SubFolder = "ConsoleDemo");

        using ServiceProvider sp = services.BuildServiceProvider();
        GenerellemOptions opts = sp.GetRequiredService<IOptions<GenerellemOptions>>().Value;

        Assert.Equal(GenerellemProfile.InMemory, opts.Profile);
        Assert.Equal(64, opts.EmbeddingDimensions);
        Assert.Equal(1200, opts.ChunkSize);
        Assert.Equal(50, opts.ChunkOverlap);
        Assert.Equal(7, opts.TopK);
        Assert.False(opts.SummarizeUserIntent);
        Assert.Equal(8, opts.EmbeddingBatchSize);
        Assert.Equal(GenerellemRunMode.Query, opts.RunMode);
        Assert.Equal(0.4f, opts.Temperature);
        Assert.Equal("ConsoleDemo", opts.SubFolder);
        Assert.Equal("from-env", opts.OllamaChatModel);
    }

    [Fact]
    public void AddGenerellem_RegistersStartupValidatorBeforeOtherHostedServices()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddHostedService<MarkerHostedService>();

        services.AddGenerellem(GenerellemProfile.InMemory);

        ServiceDescriptor firstHosted = services.First(descriptor => descriptor.ServiceType == typeof(IHostedService));
        Assert.Equal(typeof(GenerellemStartupValidator), firstHosted.ImplementationType);
    }

    [Fact]
    public void AddGenerellem_InvalidRunMode_ThrowsWhenOptionsAreBuilt()
    {
        Dictionary<string, string?> values = new()
        {
            ["Generellem:RunMode"] = "sometimes"
        };

        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new MapConfiguration(values));
        services.AddGenerellem(GenerellemProfile.InMemory);

        using ServiceProvider sp = services.BuildServiceProvider();

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => _ = sp.GetRequiredService<IOptions<GenerellemOptions>>().Value);

        Assert.Contains("RunMode", ex.Message);
    }

    [Fact]
    public void AddGenerellem_Azure_ReusesOneQdrantClient()
    {
        Dictionary<string, string?> values = new()
        {
            [GKeys.QdrantEndpoint] = "http://localhost:6334",
            [GKeys.QdrantApiKey] = "test-key",
            [GKeys.QdrantCollection] = "docs"
        };

        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new MapConfiguration(values));
        services.AddGenerellem(GenerellemProfile.Azure);

        using ServiceProvider sp = services.BuildServiceProvider();

        Assert.Same(sp.GetRequiredService<QdrantClient>(), sp.GetRequiredService<QdrantClient>());
        Assert.Same(sp.GetRequiredService<QdrantService>(), sp.GetRequiredService<QdrantService>());
        Assert.Same(sp.GetRequiredService<ISearchService>(), sp.GetRequiredService<QdrantService>());
    }

    sealed class MarkerHostedService : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    sealed class MapConfiguration(Dictionary<string, string?> values) : IConfiguration
    {
        public string? this[string key]
        {
            get => values.TryGetValue(key, out string? value) ? value : null;
            set => values[key] = value;
        }

        public IEnumerable<IConfigurationSection> GetChildren() => [];

        public IChangeToken GetReloadToken() => EmptyChangeToken.Instance;

        public IConfigurationSection GetSection(string key) => new MapSection(this, key);

        sealed class MapSection(MapConfiguration root, string path) : IConfigurationSection
        {
            public string Key => path.Split(':')[^1];

            public string Path => path;

            public string? Value
            {
                get => root[path];
                set => root[path] = value;
            }

            public string? this[string key]
            {
                get => root[path + ":" + key];
                set => root[path + ":" + key] = value;
            }

            public IEnumerable<IConfigurationSection> GetChildren() => [];

            public IChangeToken GetReloadToken() => EmptyChangeToken.Instance;

            public IConfigurationSection GetSection(string key) => new MapSection(root, path + ":" + key);
        }

        sealed class EmptyChangeToken : IChangeToken
        {
            public static readonly EmptyChangeToken Instance = new();

            public bool HasChanged => false;

            public bool ActiveChangeCallbacks => false;

            public IDisposable RegisterChangeCallback(Action<object?> callback, object? state) => EmptyDisposable.Instance;
        }

        sealed class EmptyDisposable : IDisposable
        {
            public static readonly EmptyDisposable Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
