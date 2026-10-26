using Generellem.DocumentSource;
using Generellem.Embedding;
using Generellem.Embedding.AzureOpenAI;
using Generellem.Embedding.Deterministic;
using Generellem.Embedding.Ollama;
using Generellem.Llm;
using Generellem.Llm.AzureOpenAI;
using Generellem.Llm.Local;
using Generellem.Llm.Ollama;
using Generellem.Processors;
using Generellem.Rag;
using Generellem.Repository;
using Generellem.Services;
using Generellem.Services.Azure;
using Generellem.Services.InMemory;
using Generellem.Services.Qdrant;
using Generellem.Services.Sqlite;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Generellem.DependencyInjection;

/// <summary>
/// DI registration for Generellem hosting profiles.
/// </summary>
public static class GenerellemServiceCollectionExtensions
{
    /// <summary>
    /// Registers Generellem services for the given <paramref name="profile"/>.
    /// Callers still register host-specific types such as <c>IDocumentSourceFactory</c> and hosted services.
    /// </summary>
    public static IServiceCollection AddGenerellem(
        this IServiceCollection services,
        GenerellemProfile profile,
        Action<GenerellemOptions>? configure = null)
    {
        services.AddOptions<GenerellemOptions>()
            .Configure(options =>
            {
                ApplyProfileDefaults(profile, options);
                configure?.Invoke(options);
                options.Profile = profile;
            });

        services.TryAddTransient<GenerellemContext>();
        services.TryAddTransient<IDocumentHashRepository, DocumentHashRepository>();
        services.TryAddTransient<IDynamicConfiguration, DynamicConfiguration>();
        services.TryAddTransient<IGenerellemFiles, GenerellemFiles>();
        services.TryAddTransient<IGenerellemIngestion, Ingestion>();
        services.TryAddTransient<IGenerellemQuery, AzureOpenAIQuery>();
        services.TryAddTransient<IHttpClientFactory, HttpClientFactory>();
        services.TryAddTransient<IPathProviderFactory, PathProviderFactory>();
        services.TryAddTransient<IRag, GenerellemDefaultRag>();

        switch (profile)
        {
            case GenerellemProfile.InMemory:
                services.AddSingleton<ISearchService, InMemorySearchService>();
                services.AddTransient<IEmbedding, DeterministicEmbeddingService>();
                services.AddTransient<ILlm, LocalLlmService>();
                break;

            case GenerellemProfile.Sqlite:
                services.AddTransient<ISearchService, SqliteSearchService>();
                services.AddTransient<IEmbedding, DeterministicEmbeddingService>();
                services.AddTransient<ILlm, LocalLlmService>();
                break;

            case GenerellemProfile.Ollama:
                services.AddOptions<GenerellemOptions>();
                services.AddTransient<IEmbedding, OllamaEmbedding>();
                services.AddTransient<ILlm, OllamaLlm>();
                // Search backend chosen after options are finalized at resolve time via factory.
                services.AddSingleton<InMemorySearchService>();
                services.AddTransient<SqliteSearchService>();
                services.AddTransient<ISearchService>(sp =>
                {
                    GenerellemOptions opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<GenerellemOptions>>().Value;
                    return opts.OllamaUseSqliteSearch
                        ? sp.GetRequiredService<SqliteSearchService>()
                        : sp.GetRequiredService<InMemorySearchService>();
                });
                break;

            case GenerellemProfile.Azure:
                services.AddTransient<ILlmClientFactory, LlmClientFactory>();
                services.AddTransient<IEmbedding, AzureOpenAIEmbedding>();
                services.AddTransient<ILlm, AzureOpenAILlm>();
                services.AddTransient<QdrantService>();
                services.AddTransient<AzureSearchService>();
                services.AddTransient<ISearchService>(sp =>
                {
                    GenerellemOptions opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<GenerellemOptions>>().Value;
                    return opts.UseAzureSearch
                        ? sp.GetRequiredService<AzureSearchService>()
                        : sp.GetRequiredService<QdrantService>();
                });
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(profile), profile, "Unknown Generellem profile.");
        }

        return services;
    }

    static void ApplyProfileDefaults(GenerellemProfile profile, GenerellemOptions options)
    {
        options.Profile = profile;
        options.TopK = 3;

        switch (profile)
        {
            case GenerellemProfile.InMemory:
            case GenerellemProfile.Sqlite:
                options.EmbeddingDimensions = 64;
                break;
            case GenerellemProfile.Ollama:
                options.EmbeddingDimensions = 768;
                break;
            case GenerellemProfile.Azure:
                options.EmbeddingDimensions = 1536;
                break;
        }
    }
}
