using System.Globalization;

using Generellem.Document;
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

using Qdrant.Client;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

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
    /// <remarks>
    /// Requires <see cref="IConfiguration"/> so the <c>Generellem</c> configuration section and
    /// environment overrides can be applied when options are built.
    /// </remarks>
    public static IServiceCollection AddGenerellem(
        this IServiceCollection services,
        GenerellemProfile profile,
        Action<GenerellemOptions>? configure = null)
    {
        services.AddOptions<GenerellemOptions>()
            .Configure<IConfiguration>((options, configuration) =>
            {
                ApplyProfileDefaults(profile, options);
                BindGenerellemSection(configuration.GetSection(GenerellemOptions.SectionName), options);
                configure?.Invoke(options);
                ApplyEnvironmentOverrides(configuration, options);
                options.Profile = profile;
            });

        // Run before any application hosted service so missing keys fail at startup.
        services.Insert(0, ServiceDescriptor.Singleton<IHostedService, GenerellemStartupValidator>());

        services.TryAddTransient<GenerellemContext>();
        services.TryAddTransient<IDocumentHashRepository, DocumentHashRepository>();
        services.TryAddTransient<IDynamicConfiguration, DynamicConfiguration>();
        services.TryAddTransient<IGenerellemFiles, GenerellemFiles>();
        services.TryAddTransient<IGenerellemIngestion, Ingestion>();
        services.TryAddTransient<IGenerellemQuery, AzureOpenAIQuery>();
        services.TryAddTransient<IGenerellemHttpClientFactory, GenerellemHttpClientFactory>();
        services.TryAddTransient<IPathProviderFactory, PathProviderFactory>();
        services.TryAddTransient<IRag, GenerellemDefaultRag>();
        services.TryAddTransient<TextProcessor>();
        services.TryAddTransient<LexicalReranker>();
        services.TryAddTransient<LlmReranker>();
        services.TryAddTransient<IReranker>(sp =>
        {
            GenerellemOptions opts = sp.GetRequiredService<IOptions<GenerellemOptions>>().Value;
            return opts.RerankMode switch
            {
                RerankMode.None => new PassThroughReranker(),
                RerankMode.Llm => sp.GetRequiredService<LlmReranker>(),
                _ => sp.GetRequiredService<LexicalReranker>()
            };
        });
        services.TryAddTransient<IImageTextExtractor>(sp =>
        {
            GenerellemOptions opts = sp.GetRequiredService<IOptions<GenerellemOptions>>().Value;
            if (!opts.EnableImageVision || opts.Profile is GenerellemProfile.InMemory or GenerellemProfile.Sqlite)
                return new EmbeddedImageTextExtractor();

            return ActivatorUtilities.CreateInstance<VisionImageTextExtractor>(sp);
        });

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
                services.AddSingleton<QdrantClient>(sp =>
                {
                    IDynamicConfiguration configuration = sp.GetRequiredService<IDynamicConfiguration>();
                    return QdrantClientFactory.Create(
                        configuration[GKeys.QdrantEndpoint],
                        configuration[GKeys.QdrantApiKey]);
                });
                services.AddSingleton<QdrantService>();
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
        options.ChunkSize = 5000;
        options.ChunkOverlap = 100;
        options.Temperature = 0;

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

    static void BindGenerellemSection(IConfigurationSection section, GenerellemOptions options)
    {
        if (int.TryParse(section[nameof(GenerellemOptions.EmbeddingDimensions)], out int embeddingDimensions))
            options.EmbeddingDimensions = embeddingDimensions;

        if (int.TryParse(section[nameof(GenerellemOptions.ChunkSize)], out int chunkSize))
            options.ChunkSize = chunkSize;

        if (int.TryParse(section[nameof(GenerellemOptions.ChunkOverlap)], out int chunkOverlap))
            options.ChunkOverlap = chunkOverlap;

        if (int.TryParse(section[nameof(GenerellemOptions.TopK)], out int topK))
            options.TopK = topK;

        if (bool.TryParse(section[nameof(GenerellemOptions.SummarizeUserIntent)], out bool summarizeUserIntent))
            options.SummarizeUserIntent = summarizeUserIntent;

        if (int.TryParse(section[nameof(GenerellemOptions.EmbeddingBatchSize)], out int embeddingBatchSize))
            options.EmbeddingBatchSize = embeddingBatchSize;

        string? runModeText = section[nameof(GenerellemOptions.RunMode)];
        if (!string.IsNullOrWhiteSpace(runModeText))
        {
            if (!GenerellemRunModeParser.TryParse(runModeText, out GenerellemRunMode runMode))
                throw new InvalidOperationException(
                    $"Generellem:RunMode '{runModeText}' is invalid. Use Both, Ingest, or Query.");

            options.RunMode = runMode;
        }

        if (float.TryParse(
                section[nameof(GenerellemOptions.Temperature)],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float temperature))
            options.Temperature = temperature;

        string? subFolder = section[nameof(GenerellemOptions.SubFolder)];
        if (subFolder is not null)
            options.SubFolder = subFolder;

        string? ollamaEndpoint = section[nameof(GenerellemOptions.OllamaEndpoint)];
        if (!string.IsNullOrWhiteSpace(ollamaEndpoint))
            options.OllamaEndpoint = ollamaEndpoint;

        string? ollamaChat = section[nameof(GenerellemOptions.OllamaChatModel)];
        if (!string.IsNullOrWhiteSpace(ollamaChat))
            options.OllamaChatModel = ollamaChat;

        string? ollamaEmbed = section[nameof(GenerellemOptions.OllamaEmbeddingModel)];
        if (!string.IsNullOrWhiteSpace(ollamaEmbed))
            options.OllamaEmbeddingModel = ollamaEmbed;

        if (bool.TryParse(section[nameof(GenerellemOptions.OllamaUseSqliteSearch)], out bool ollamaUseSqlite))
            options.OllamaUseSqliteSearch = ollamaUseSqlite;

        if (bool.TryParse(section[nameof(GenerellemOptions.UseAzureSearch)], out bool useAzureSearch))
            options.UseAzureSearch = useAzureSearch;

        string? rerankMode = section[nameof(GenerellemOptions.RerankMode)];
        if (!string.IsNullOrWhiteSpace(rerankMode))
        {
            if (!Enum.TryParse(rerankMode, ignoreCase: true, out RerankMode mode))
                throw new InvalidOperationException(
                    $"Generellem:RerankMode '{rerankMode}' is invalid. Use None, Lexical, or Llm.");

            options.RerankMode = mode;
        }

        if (bool.TryParse(section[nameof(GenerellemOptions.EnableImageVision)], out bool enableImageVision))
            options.EnableImageVision = enableImageVision;

        string? visionModel = section[nameof(GenerellemOptions.VisionModel)];
        if (visionModel is not null)
            options.VisionModel = visionModel;
    }

    static void ApplyEnvironmentOverrides(IConfiguration configuration, GenerellemOptions options)
    {
        string? envEndpoint = configuration[GKeys.OllamaEndpoint];
        if (!string.IsNullOrWhiteSpace(envEndpoint))
            options.OllamaEndpoint = envEndpoint;

        string? envChat = configuration[GKeys.OllamaChatModel];
        if (!string.IsNullOrWhiteSpace(envChat))
            options.OllamaChatModel = envChat;

        string? envEmbed = configuration[GKeys.OllamaEmbeddingModel];
        if (!string.IsNullOrWhiteSpace(envEmbed))
            options.OllamaEmbeddingModel = envEmbed;
    }
}
