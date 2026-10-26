using Generellem.DependencyInjection;
using Generellem.Embedding;
using Generellem.Embedding.Deterministic;
using Generellem.Llm;
using Generellem.Llm.Local;
using Generellem.Services;
using Generellem.Services.InMemory;
using Generellem.Services.Sqlite;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
}
